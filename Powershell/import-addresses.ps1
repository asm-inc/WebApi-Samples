$instance = ""
$accessKey = ""
$secret = ""
$facility = ""


class Query {
    [string]$source
    [Array]$fields
    [Object]$filter
    [Object]$sort
}
class MDStaffApi {
    [string]$BaseUrl
    [string]$Instance
    [string]$ClientID
    [string]$ClientSecret
    [string]$Facility
    [Object]$Token

    MDStaffApi([string]$instance, [string]$client_id, [string]$client_secret, [string]$facility) {
        $this.Instance = $instance
        $this.ClientID = $client_id
        $this.ClientSecret = $client_secret
        $this.Facility = $facility
        $this.BaseUrl = "api.asm-cloud.com/api"
    }

    [void]refreshToken() {
        $now = Get-Date
        # Check to see if the token is null
        If($null -ne $this.Token -and $this.Token.expires_at -gt $now) {
            Write-Host "Not refreshing token, token expires at $($this.Token.expires_at)"
            return # Token is not expired, leave and don't refresh
        }

        # Define the target URL
        $url = "https://$($this.Instance).$($this.BaseUrl)/tokens"
    
        # Prepare the data as a hashtable
        $data = @{
            grant_type = "client_credentials"
            client_id = $this.ClientID
            client_secret = $this.ClientSecret
            scope = "$($this.Instance)/$($this.Facility)"
        }
    
        # Convert the hashtable to a URL-encoded string
        $formData = ($data.Keys | ForEach-Object {
            $key = [uri]::EscapeDataString($_) # URL-encode the key
            $value = [uri]::EscapeDataString($data[$_]) # URL-encode the value
            "$key=$value"
        }) -join "&" # link params together with &
    
        # Send the POST request
        $response = Invoke-WebRequest -Uri $url -Method POST -Body $formData -ContentType "application/x-www-form-urlencoded"
    
        # Display the response
        $tokenObj = ConvertFrom-Json $response.Content
    
        $exp = (Get-Date).AddSeconds($tokenObj.expires_in).AddSeconds(-10)
        $tokenObj | Add-Member NoteProperty -Name "expires_at" -Value $exp
        
        Write-Host $exp
        $tokenObj.expires_at = $exp
        Write-Host "$($tokenObj.expires_at)"
        $this.Token = $tokenObj
    }
    [Object]queryFromObject([Query]$queryObj) {
        $json = ConvertTo-Json $queryObj -Depth 4
        Write-Host $json
        return $this.query($json)
    }

    [Object]query([string]$jsonString) {
        return $this.post("/query", $jsonString)
    }

    [Object]post([string]$urlPath, [string]$bodyJsonString) {
        # Refresh the token if needed
        $this.refreshToken()
        # Run the query
        $url = "https://$($this.Instance).$($this.BaseUrl)/$($this.Instance)$urlPath"
        Write-Host "POST $url"
        $headers = @{
            Authorization = "Bearer $($this.Token.access_token)"
        }

        $response = Invoke-WebRequest -Uri $url -Method POST -Headers $headers -Body $bodyJsonString -ContentType "application/json"
        $data = $response.Content
        #Write-Host $data
        return ConvertFrom-Json $data
    }
}


$api = [MDStaffApi]::new($instance, $accessKey, $secret, $facility)
$api.refreshToken()
$api.refreshToken()

$query = [Query]::new()
$query.source = "Demographic"
$query.fields = @("LastName", "FirstName", "NPI")
$query.filter = [PSCustomObject]@{
    NPI = @( [PSCustomObject]@{ 
        type = "not in"
        values = @($null)
    } ) }
$query.sort = @(
    [PSCustomObject]@{ LastName = "ASC" },
    [PSCustomObject]@{ FirstName = "ASC" }
)



$results = $api.queryFromObject($query)
Write-Host "$($results[0].FirstName) $($results[0].LastName): $($results[0].NPI)"


#$token = authenticate "asm-venus" -client_id "Import-Test" -client_secret "xKoNxfdNByWx0NGqebpYH0wg1clJMNkak3oLUj0F" -facility "501A1D4F-EFE8-40EC-ACA7-4DDDC39329E0"
#Write-Host $token

#$data = Import-Csv -Path "data/provider-address.csv"