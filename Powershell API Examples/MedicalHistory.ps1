<#
	Summary: This example utilizes a CSV and performs check exist logic based on the NPI & Medical History Type provided. 
	If all conditions are met, then all unique MedicalHistory records will written in batched requests to the '/object/medicalhistory/batch' endpoint.
	
	Conditions for Upsert to occur:
	1. There must not be more than 1 Provider record in the system tied to the row item NPI provided in the CSV. 
	2. There must not be more than 1 MedicalHistory record in the system tied to row item NPI and MedicalHistoryType in the CSV. 
	
	Prerequisites:
	1. Verify the example NPIs in the CSV match existing Provider records in your MD-Staff environment. Provider records will not be created if a match on NPI cannot be established.
	2. Verify the NPIs you provide in the CSV associate to a Provider record in your MD-Staff environment. Additionally, must be within context of your Authorization Token scope.
	3. Verify the MedicalHistoryTypes provided in the CSV match existing MedicalHistory LookUps(matched on Code value).
#>

#Include library files
. "lib/mdstaffapi.ps1"

#Authorization Token Parameters: https://support.asm-inc.com/hc/en-us/articles/115013514388-Obtaining-Web-API-Authorization-Token
$instance = "{your-instance-code}"
$facility = "{your-facilityid-or-marketid}"
$SecureCredential = Get-Credential

#Configure number of records per request sent to Object/Address/Batch endpoint. For rate limits: https://support.asm-inc.com/hc/en-us/articles/10119690240923-Request-and-Data-Rate-Limits
$recordsPerBatch = 35 

#Create instance of MDStaff API Helper
$api = [MDStaffApi]::new($instance, $SecureCredential.UserName, $SecureCredential.Password, $facility)

Function FindProvider() {
    param (
        [string]$npi
    )
    $query = [Query]::new()
    $query.source = "Appointment"
    $query.fields = @("ProviderID", "ProviderID.LastName", "ProviderID.FirstName", "ProviderID.NPI")
    $query.filter = [PSCustomObject]@{
        "ProviderID.NPI" = @( [PSCustomObject]@{ 
            type = "in"
            values = @($npi)
        } ) }
	
	#If you want to include archived Providers, override IncludeArchivedProviders to "true"
	$query.settings = [PSCustomObject]@{
        IncludeArchivedProviders = "false"}

    $provider = $api.queryFromObject($query)
	
	If($provider.length -gt 1)
	{
		return "false"
	}
	
    return $provider[0].ProviderID
}

Function FindMedicalHistory() {
    param (
        [string]$providerID,
		[string]$medicalHistoryTypeID
    )
    $query = [Query]::new()
    $query.source = "MedicalHistory"
    $query.fields = @("Uid")
    $query.filter = [PSCustomObject]@{
        "ProviderID" = @( [PSCustomObject]@{ 
            type = "in"
            values = @($providerID)
        } )
		"MedicalHistoryTypeID.Code" = @( [PSCustomObject]@{ 
			type = "in"
			values = @($medicalHistoryTypeID)
		} )
	}
	
	#If you want to include archived Providers, override IncludeArchivedProviders to "true"
	$query.settings = [PSCustomObject]@{
        IncludeArchivedProviders = "false"}

    $medicalHistory = $api.queryFromObject($query)
	
	If($medicalHistory.length -gt 1)
	{
		return "false"
	}
	
    return $medicalHistory[0].Uid
}

$data = Import-Csv -Path "data/medical-history.csv"
$batchCounter = 0
$Counter = 0
$Total = $data.count
[Array]$currentArray
[Array]$tempArray
[string]$foundExists
[string]$currentBatch

foreach ($row in $data) {
    $providerID = FindProvider $row.NPI
	$Counter++
	#Found Provider Matching NPI value in current row
	If($providerID -ne "false" -and $providerID -ne "" -and $null -ne $providerID){
	
		$medicalHistoryID = FindMedicalHistory $providerID $row.MedicalHistoryTypeID
		Write-Host "ID: $medicalHistoryID ProviderID: $providerID TypeID"

		#If MedicalHistoryID is found and no duplicates, will be an UPDATE
		If($medicalHistoryID -ne "false" -and $medicalHistoryID -ne "" -and $null -ne $medicalHistoryID){
			$row | Add-Member NoteProperty -Name "id" -Value $medicalHistoryID
			$row | Add-Member NoteProperty -Name "ProviderID" -Value $providerID
			$row.PSObject.Properties.Remove('NPI')
			
			ForEach ($Item in $currentArray) {
				If ($Item.ProviderID -eq $providerID -and $Item.MedicalHistoryTypeID -eq $row.MedicalHistoryTypeID) {
					$tempArray += @($Item)
				}
			}

			if($tempArray.length -eq 0){
				$currentArray += @($row)
				$batchCounter++
			}
			
			$tempArray = $null
		}
		ElseIf($null -eq $medicalHistoryID -or $medicalHistoryID -eq ""){
			Write-Host "Unable to Find Existing Medical History: Will be an INSERT"
			$row | Add-Member NoteProperty -Name "ProviderID" -Value $providerID
			$row.PSObject.Properties.Remove('NPI')
						
			ForEach ($Item in $currentArray) {
				If ($Item.ProviderID -eq $providerID -and $Item.MedicalHistoryTypeID -eq $row.MedicalHistoryTypeID) {
					$tempArray += @($Item)
				}
			}

			if($tempArray.length -eq 0){
				$currentArray += @($row)
				$batchCounter++
			}
			
			$tempArray = $null
		}
		Else{
			Write-Host "Multiple MedicalHistory record tied to same NPI: Removed MedicalHistory from $row batch"
		}
	}
	#Unable to match to a Provider via the NPI value in current row
	ElseIf($null -eq $providerID){
		Write-Host "Unable to Find Provider: Removed MedicalHistory $row from batch"
	}
	#There are more than one Provider record with the given NPI in current row
	Else{
		Write-Host "Multiple Providers with same NPI: Removed MedicalHistory from $row batch"
	}
	
	#Last iteration
	If($Total -le $recordsPerBatch -and $batchCounter -ne 0 -and $Counter -eq $Total ) {
		If($currentArray.count -eq 1){
			$currentBatch = $currentArray | ConvertTo-Json 
			$currentBatch = "[" + $currentBatch + "]"
		}
		Else{
			$currentBatch = $currentArray | ConvertTo-Json 
		}

		Write-Host $currentBatch #Uncomment to debug the current batch
		$savedMedicalHistory = $api.post("/object/MedicalHistory/batch", $currentBatch)
		
		$batchCounter = 0
		$currentBatch = $null
		$currentArray = $null
	}
	
		#Last iteration
	If($batchCounter -le $recordsPerBatch -and $batchCounter -ne 0 -and $Counter -eq $Total) {
		If($currentArray.count -eq 1){
			$currentBatch = $currentArray | ConvertTo-Json 
			$currentBatch = "[" + $currentBatch + "]"
		}
		Else{
			$currentBatch = $currentArray | ConvertTo-Json 
		}

		Write-Host $currentBatch #Uncomment to debug the current batch
		$savedMedicalHistory = $api.post("/object/MedicalHistory/batch", $currentBatch)
		
		$batchCounter = 0
		$currentBatch = $null
		$currentArray = $null
	}
	
	#Full Batch
	If($batchCounter -eq $recordsPerBatch ){
		$currentBatch = $currentArray | ConvertTo-Json 
		Write-Host $currentBatch #Uncomment to debug the current batch
		$savedMedicalHistory = $api.post("/object/MedicalHistory/batch", $currentBatch)
		$batchCounter = 0
		$currentBatch = $null
		$currentArray = $null
	}	
		
	Write-Host "6-Second Delay for adhering to API Rate Limits"
	Start-Sleep -Seconds 8
}

Write-Host "Done"