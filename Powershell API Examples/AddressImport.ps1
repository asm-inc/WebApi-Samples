<#
	Summary: This example utilizes a CSV and always creates Address records that can be matched to existing Provider's by NPI.  
	If all conditions are met, then all valid Address records from the CSV will be imported in batched requests to the '/object/address/batch' endpoint.
	
	Conditions for Upsert to occur:
	1. There must not be more than 1 Provider record in the system tied to the row item NPI provided in the CSV. 

	Prerequisites:
	1. Verify the example NPIs in the CSV match existing Provider records in your MD-Staff environment. Provider records will not be created if a match on NPI cannot be established.
	2. Verify the NPIs you provide in the CSV associate to a Provider record in your MD-Staff environment. Additionally, must be within context of your Authorization Token scope.
#>
#Include library files
. "lib/mdstaffapi.ps1"

#Authorization Token Parameters: https://support.asm-inc.com/hc/en-us/articles/115013514388-Obtaining-Web-API-Authorization-Token
$instance = "{your-instance-code}"
$facility = "{your-facilityid-or-marketid}"
$SecureCredential = Get-Credential

#Configure number of records per request sent to Object/Address/Batch endpoint. For rate limits: https://support.asm-inc.com/hc/en-us/articles/10119690240923-Request-and-Data-Rate-Limits
$recordsPerBatch = 20 

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

$data = Import-Csv -Path "data/provider-address.csv"
$batchCounter = 0
$Counter = 0
$Total = $data.count
[Array]$currentArray
[string]$currentBatch

foreach ($row in $data) {
    $providerID = FindProvider $row.NPI
	$Counter++
	#Found Provider Matching NPI value in current row
	If($providerID -ne "false" -and $providerID -ne "" -and $null -ne $providerID){
		$row | Add-Member NoteProperty -Name "ProviderID" -Value $providerID
		$row.PSObject.Properties.Remove('NPI')
		$currentArray += @($row)
		$batchCounter++
	}
	#Unable to match to a Provider via the NPI value in current row
	ElseIf($null -eq $providerID){
		Write-Host "Unable to Find Provider: Removed Address $row from batch"
	}
	#There are more than one Provider record with the given NPI in current row
	Else{
		Write-Host "Multiple Providers with same NPI: Removed Address from $row batch"
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
		$savedAddress = $api.post("/object/Address/batch", $currentBatch)
		
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
		$savedAddress = $api.post("/object/Address/batch", $currentBatch)
		
		$batchCounter = 0
		$currentBatch = $null
		$currentArray = $null
	}
	
	#Full Batch
	If($batchCounter -eq $recordsPerBatch ){
		$currentBatch = $currentArray | ConvertTo-Json 
		Write-Host $currentBatch #Uncomment to debug the current batch
		$savedAddress = $api.post("/object/Address/batch", $currentBatch)
		$batchCounter = 0
		$currentBatch = $null
		$currentArray = $null
	}	
		
	Write-Host "6-Second Delay for adhering to API Rate Limits"
	Start-Sleep -Seconds 6
}

Write-Host "Done"