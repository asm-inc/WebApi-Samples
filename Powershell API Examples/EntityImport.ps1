<#
	Summary: This example utilizes a CSV and only creates Entity records that don't currently exist based on the matching criteria of Name + TaxIDNumber.  
	If all conditions are met, then all valid Entity records from the CSV will be imported in batched requests to the '/object/entity/batch' endpoint.
	
	Conditions for Upsert to occur:
	1. There must not be more than 1 Entity record in the system tied to the row item Entity Name + TaxIDNUmber provided in the CSV. 

	Prerequisites:
	1. Verify the Entities provided in the CSV do not associate to an existing Entity in your MD-Staff environment. Any matches will not be eligible for import.
#>

#Include library files
. "lib/mdstaffapi.ps1"

#Authorization Token Parameters: https://support.asm-inc.com/hc/en-us/articles/115013514388-Obtaining-Web-API-Authorization-Token
$instance = "{your-instance-code}"
$facility = "{your-facilityid-or-marketid}"
$SecureCredential = Get-Credential

#Configure number of records per request sent to Object/Entity/Batch endpoint. For rate limits: https://support.asm-inc.com/hc/en-us/articles/10119690240923-Request-and-Data-Rate-Limits
$recordsPerBatch = 25 

#Create instance of MDStaff API Helper
$api = [MDStaffApi]::new($instance, $SecureCredential.UserName, $SecureCredential.Password, $facility)

Function FindEntity() {
    param (
        [string]$entityName,
		[string]$entityTaxIDNumber
    )
    $query = [Query]::new()
    $query.source = "Entity"
    $query.fields = @("EntityID", "TaxIDNumber", "NPI", "Name")
	
	if($entityTaxIDNumber -eq $null -or $entityTaxIDNumber -eq ""){
		$query.filter = [PSCustomObject]@{
			"Name" = @( [PSCustomObject]@{ 
				type = "in"
				values = @($entityName)
			} )
		}
	}
	else{
		$query.filter = [PSCustomObject]@{
			"Name" = @( [PSCustomObject]@{ 
				type = "in"
				values = @($entityName)
			} )
			"TaxIDNumber" = @( [PSCustomObject]@{ 
				type = "in"
				values = @($entityTaxIDNumber)
			} )
		}
	}
    
	#If you want to include archived Providers, override IncludeArchivedProviders to "true"
	$query.settings = [PSCustomObject]@{
        IncludeArchivedProviders = "false"}

    $entity = $api.queryFromObject($query)
	
	If($entity.length -gt 1)
	{
		return "false"
	}
	
    return $entity[0].EntityID
}

$data = Import-Csv -Path "data/entities.csv"
$batchCounter = 0
$Counter = 0
$Total = $data.count
[Array]$currentArray
[string]$currentBatch

foreach ($row in $data) {
    $entityID = FindEntity $row.Name $row.TaxIDNumber
	$Counter++
	#Found Provider Matching Entity
	If($entityID -ne "false" -and $entityID -ne "" -and $null -ne $entityID){
		Write-Host "Found existing Entity matching on Name & TaxIDNumber: Removed Entity $row from batch"
	}
	#Unable to find existing Entity with same name and TaxIDNumber in current row. Create
	ElseIf($null -eq $entityID -or $entityID -eq ""){
		$currentArray += @($row)
		$batchCounter++
	}
	#There are more than one Entity record with the given Name & TaxIDNumber in current row
	Else{
		Write-Host "Multiple existing Entities with same Name & TaxID: Removed Entity from $row batch"
	}
	
	#Last iteration
	If($Total -le $recordsPerBatch -and $batchCounter -ne 0 -and $Counter -eq $Total) {
		If($currentArray.count -eq 1){
			$currentBatch = $currentArray | ConvertTo-Json 
			$currentBatch = "[" + $currentBatch + "]"
		}
		Else{
			$currentBatch = $currentArray | ConvertTo-Json 
		}
		
		Write-Host $currentBatch #Uncomment to debug the current batch
		$savedEntity = $api.post("/object/Entity/batch", $currentBatch)
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
		$savedEntity = $api.post("/object/Entity/batch", $currentBatch)
		$batchCounter = 0
		$currentBatch = $null
		$currentArray = $null
	}
	
	#Full Batch
	If($batchCounter -eq $recordsPerBatch){
		$currentBatch = $currentArray | ConvertTo-Json 
		Write-Host $currentBatch #Uncomment to debug the current batch
		$savedEntity = $api.post("/object/Entity/batch", $currentBatch)
		$batchCounter = 0
		$currentBatch = $null
		$currentArray = $null
	}
	
	Write-Host "6-Second Delay for adhering to API Rate Limits"
	Start-Sleep -Seconds 6
}

Write-Host "Done"