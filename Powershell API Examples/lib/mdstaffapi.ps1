class Query {
    [string]$source
    [Array]$fields
    [Object]$filter
    [Object]$sort
	[Object]$settings
}

class ProviderFile {
    [string]$providerID
    [string]$providerFileData

	ProviderFile([string]$provider_id, [string]$provider_file_data) {
        $this.providerID = $provider_id
        $this.providerFileData = $provider_file_data
    }
}

class PDFFile {
    [string]$PrimaryID
    [string]$FileName

	PDFFile([string]$primary_ID, [string]$fileName) {
        $this.PrimaryID = $primary_ID
        $this.FileName = $fileName
    }
}

class MDStaffApi {
    [string]$BaseUrl
    [string]$Instance
    [string]$ClientID
    [string]$ClientSecret
    [string]$Facility
    [Object]$Token
	[bool]$LastTokenFailed
	[int]$TokenFailureCount
	
    MDStaffApi([string]$instance, [string]$client_id, [Security.SecureString]$client_secret, [string]$facility) {
        $this.Instance = $instance
        $this.ClientID = $client_id
        $this.ClientSecret = $this.decryptSecret($client_secret)
        $this.Facility = $facility
		$this.LastTokenFailed = $false
		$this.TokenFailureCount = 0
        $this.BaseUrl = "api.asm-cloud.com/api"
    }

    [void]refreshToken() {
        $now = Get-Date

        # Check to see if the token is null
        If($null -ne $this.Token -and $this.Token.expires_at -gt $now) {
            Write-Host "Reusing Auth Token, token expires at $($this.Token.expires_at)"
            return # Token is not expired, leave and don't refresh
        }
		
		if($this.LastTokenFailed -and $this.TokenFailureCount -eq 10){
			Write-Host "Maximum Token Retry attempts reached. Please check your API credentials and/or verify permissions. Exiting the script."
			exit 1
		}

        # Define the target URL
        $url = "https://$($this.Instance).$($this.BaseUrl)/tokens"
    
		#$decrypted_client_secret = $this.decryptSecret($this.ClientSecret)
		#Write-Host $decrypted_client_secret
		
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
    
        # Get the Token	
		try{
			$response = Invoke-WebRequest -Uri $url -Method POST -Body $formData -ContentType "application/x-www-form-urlencoded"
			
			#Send URL to console
			Write-Host "POST $url"
			
			# Display the response
			$tokenObj = ConvertFrom-Json $response.Content
		
			$exp = (Get-Date).AddSeconds($tokenObj.expires_in).AddSeconds(-10)
			$tokenObj | Add-Member NoteProperty -Name "expires_at" -Value $exp
			
			Write-Host $exp
			$tokenObj.expires_at = $exp
			Write-Host "$($tokenObj.expires_at)"
			$this.Token = $tokenObj
			$this.LastTokenFailed = $false;
			$this.TokenFailureCount = 0
		}
		catch [System.Net.WebException]{
			$errorResponse = $_.Exception.Response
			
			if($errorResponse -and ($errorResponse.StatusCode -eq 400 -or $errorResponse.StatusCode -eq 401)){
				Write-Host "Unauthorized Access, failed to Obtain an Authorization Token. Token retry attempt commencing in 10 seconds."
			}
			else{
				Write-Host "An error occurred: $($_.Exception.Message)"
			}	
			
			$this.LastTokenFailed = $true
			$this.TokenFailureCount++
			Start-Sleep -Seconds 10
			$this.refreshToken()
		}  
    }
	
	[Object]decryptSecret([Object]$secret) {
		$Ptr = [System.Runtime.InteropServices.Marshal]::SecureStringToCoTaskMemUnicode($secret)
		$result = [System.Runtime.InteropServices.Marshal]::PtrToStringUni($Ptr)
        
        return $result
    }
	
	#Send Request to Query endpoint via object
    [Object]queryFromObject([Query]$queryObj) {
		$json = ConvertTo-Json $queryObj -Depth 4
        
		#Write-Host $json #Uncomment to see actual JSON request body (for debugging purposes)
        return $this.query($json)
    }

	#Send Request to Query endpoint via json string
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
	
	[Object]postObject([string]$urlPath, [PsCustomObject]$bodyJsonString) {
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
        return $data
    }
}
