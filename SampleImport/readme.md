# Objective of Sample App
Accept a CSV in a defined specification from a directory accessible to the app, load/process the CSV, and import the Provider & Insurance data into MD-Staff over the Web API.  

## CSV Specifications

1. The CSV must have a header row and be text qualified(with double quotes). E.g. "FirstName"
2. All rows of data must be text qualified(with double quotes). E.g. "John"
2. ```*``` denotes a required field 
3. Accepts 15 columns listed below and **must** be in the following order.
	1. ```*```FirstName
	2. ```*```LastName
	3. ```*```NPI
	4. ```*```InsuranceCarrier
	5. Address
	6. Address2
	7. City
	8. State
	9. ZipCode
	10. Country
	11. Telephone
	12. PolicyNumber
	13. Coverage
	14. IssuedDate
	15. Expires

### Loading Data
The Console App will do it's best to take the provided CSV rows and load it into the schema/models creating for Providers and Provider Insurance records.


## Configuring appsettings.json

#### ```WebAPIConnection```
Manages connection to Web API and control various behaviors in the Console App.

* **APIUrl** - ```https://{instanceCode}.api.asm-cloud.com/```
* **GrantType** - ```client_credentials```
* **ClientId** - API Access Key username for your connection to the Web API.
* **ClientSecret** - API secret for your API Access Key.
* **InstanceCode** - Can be obtained in MD-Staff by navigating to Help > About MD-Staff.
* **FacilityOrMarketID** - Recommended that you scope to a Market or global market. That way the matching and searching logic can span across the entire system and not have a limited scope.
* **ImportIntoFacilityID** - Facility you want new Providers imported into (Appointment Facility association). Imported Insurance Records will always be ```global```.
* **Debug** - ```true``` will allow details from the Web API related steps to be outputted to the console. ```false``` will disable outputting details to the console.


#### ```FileImport```
Manages location to CSV file and control various behaviors in the Console App.

* **FilePath** - path where the CSV is located. Directory must be accessible to the Console App.
* **FileName** - name of the CSV file.
* **Debug** - ```true``` will allow details from the File Loading steps to be outputted to the console. ```false``` will disable outputting details to the console.




## Conditions for Skipping a Record
1. If a row in the CSV does not meet the defined specification, the row will automatically be skipped.
2. If more than 1 Provider is located in MD-Staff matching details provided in the CSV(```"FirstName"```, ```"LastName"```, ```"NPI"```), the  row will be skipped and not imported. 
3. If an existing Insurance record is found in MD-Staff for a matched provider, it will be skipped and not imported.
	
### Behavior Review of Sample App

1. Load rows from the CSV file into a Collection(loaded into Memory) within the Console App.
	
2. Obtain an Authorization Token
	* Built-in retry logic should the Authorization Token expire or something else goes wrong. Retry logic is to attempt 3 times, then wait 60 seconds before the attempting to obtain a Token again. If obtaining a Token fails 21 times in a row, the Console app is terminated, 
	
3. Iterate through all of the succesfully loaded rows one by one. Every HTTP request sent to the Web API is tracked to ensure rate limits are not exceeded.

### Expanding on 3. Behavior(s) for Processing/Importing Provider

#### Logic for locating a Provider
Using the Provider Details provided(```"FirstName"```, ```"LastName"```, ```"NPI"```), send HTTP request to the ```Query``` endpoint targeting the Demographic data object to locate a provider matching those exact details in MD-Staff. 

If match is found, move onto reviewing/processing the Insurance record provided for this provider.

If no match is found, secondary HTTP request is sent to the ```Query``` endpoint targeting the Demographic data object to locate a provider matching on exact details provided for ```"LastName"``` and ```"NPI"```. 


#### Creating a New Provider
If no matching Provider is found from primary and secondary attempt, this indicates the Provider will be created in MD-Staff. 

1. A HTTP request is sent to ```Object/Demographic``` to create a new Demographic record using the ```"FirstName"```, ```"LastName"```, and ```"NPI"``` provided. 

2. A subsequent HTTP request will be sent to ```Object/Appointment``` to create a new Appointment record. The FacilityID will be scoped to the same FacilityID recorded in the ```WebAPIConnection:ImportIntoFacilityID``` app setting.



### Expanding on 3. Behavior(s) for Processing/Importing Insurance

#### Logic for locating an Insurance record

Using the ```ProviderID``` from the matched/created Provider record, ```InsuranceCarrier```, ```Expires``` in CSV, and defaulting to InUse Insurance records, the app will attempt to find a match.

**If match is found, the Insurance row from the CSV will not be imported.**

If no match is found, secondary HTTP request is sent to the ```Query``` endpoint targeting the ReferenceSource data object to determine if there's an existing ReferenceSource with a ReferenceType of ```Insurance Carrier``` matching the value from the CSV field ```InsuranceCarrier```. 
* If a ReferenceSource is found, no ReferenceSource record will be created. But we will store and use the matched ReferenceSourceID when creating the Insurance record.
* If a ReferenceSource is not found, a ReferenceSource of type ```Insurance Carrier``` will be created. The ReferenceSourceID resulting from the newly created ReferenceSource will be used when creating the Insurance record.


#### Creating a New Provider Insurance
If no matching Insurance record is found, this indicates a Provider Insurance will be created in MD-Staff. 

1. A HTTP request is sent to ```Object/Insurance``` to create a new Insurance record using the fields from the CSV ```"PolicyNumber"```, ```"Coverage"```,```"IssuedDate"```, and ```"Expires"``` provided in the CSV and the ReferenceSourceID + ProviderID matched in previous steps. 
