<#
	Summary: This example takes a directory in this solution filled with files that follow a specific File naming convention (including Demographic.OtherID). 
	If all conditions are met, then all valid ProviderFiles that can be matched to a Provider (from the directory) will be imported one by one via the '/providers/{providerid}/providerfile' endpoint.
	
	Condition for ProviderFile upload to occur:
	1. There must be a Demographic record in MD-Staff with a matching OtherID value that has been included in the file naming. 
	2. The file extension and type must be valid.

	Potential Use-case:
	* If you have a task to upload multiple ProviderFiles into MD-Staff and want to use the Web API to automate this process. 
	
	Notes:
	* Each ProviderFile being uploaded to MD-Staff may take approximately 15 seconds with the explicit delays introduced between requests(for rate limiting).
	* E.g. You can safely upload approximately 5 ProviderFiles per minute, 300 per hour, 7,200 per day.
#>

#Include library files
. "Lib/mdstaffapi.ps1"

$delayBetweenRequests = 6
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$providerFileFolderPath  = Join-Path -Path $scriptPath -ChildPath \providerfiles
Set-Location $scriptPath

#Authorization Token Parameters: https://support.asm-inc.com/hc/en-us/articles/115013514388-Obtaining-Web-API-Authorization-Token
$instance = "{your-instance-code}"
$facility = "{your-facilityid-or-marketid}"

$SecureCredential = Get-Credential

#Create instance of MDStaff API Helper
$api = [MDStaffApi]::new($instance, $SecureCredential.UserName, $SecureCredential.Password, $facility)

Function FindProvider() {
    param (
        [string]$npi
    )
    $query = [Query]::new()
    $query.source = "Demographic"
    $query.fields = @("ProviderID")
    $query.filter = [PSCustomObject]@{
        "OtherID" = @( [PSCustomObject]@{ 
            type = "in"
            values = @($npi)
        } ) }
	
	#If you want to include archived Providers, override IncludeArchivedProviders to "true"
	$query.settings = [PSCustomObject]@{
        IncludeArchivedProviders = "false"}

    $provider = $api.queryFromObject($query)
	
    return $provider[0].ProviderID
}

Function GetFileBase64() {
    param (
        [string]$filePath
    )
    
    # Read the file as bytes
    $fileBytes = [System.IO.File]::ReadAllBytes($filePath)

    # Convert the byte array to a Base64 string
    $base64String = [Convert]::ToBase64String($fileBytes)
	
    return $base64String
}

$prefixArray = @()

# Loop through all files in the directory
Get-ChildItem -Path $providerFileFolderPath -File | ForEach-Object {
    # Use regex to extract the numeric prefix
    if ($_.Name -match "^(\d+)_") {
        $prefix = $matches[1]
        # Add the numeric prefix to the array
        $prefixArray += [PDFFile]::new($prefix, $_.Name)
    }
}

[Array]$currentArray = @()

foreach ($number in $prefixArray) {
	$providerID = FindProvider $number.PrimaryID
	$fileName = $providerFileFolderPath + "\" + $number.FileName
    	
	$providerFileData = GetFileBase64 $fileName
	
	if($providerID -ne $null -and $fileName -ne $null -and $providerFileData -ne $null){
		$currentArray += [ProviderFile]::new($($providerID), $($providerFileData))
	}

    Start-Sleep -Seconds $delayBetweenRequests
}

foreach ($provFile in $currentArray) {
	$headers=@{}
    $headers.Add("Content-Type", "application/json")
    $headers.Add("Authorization", "Bearer $($api.Token.access_token)")
	$url = "/providers/$($provFile.providerID)/providerfile"
	
	$responseBody = [PsCustomObject]@{ 
		"FileData" = "$($provFile.providerFileData)"
		"FileDescription" = "{your-file-description}"
		"FileExtension" = ".pdf"
		"FileTypeId" = "{your-file-type-id}"
	} | ConvertTo-Json
	
	$response = $api.postObject($url, $responseBody)
	Start-Sleep -Seconds $delayBetweenRequests
}
