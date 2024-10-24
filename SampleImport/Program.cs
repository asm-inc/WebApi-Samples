// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Configuration;
using SampleImport.Utilities;

Console.WriteLine("Starting up...");

/*ImportHelper Handles:
 * STEP 1: Load the config keys
 * STEP 2: Load the WebAPI & FileImport Setting Keys
 * STEP 3: Load the CSV Data into Collection/Memory
 */

ImportHelper importHelper = new ImportHelper();

/*STEP 4: Get Auth Token*/
await importHelper.GetAuthorizationToken();

/*START IMPORT*/
/*STEP 5: Check If Provider Exists(Query endpoint using FirstName, LastName and NPI)*/
var results = await importHelper.ImportData(importHelper.dataRowCollection);

Console.WriteLine("\nImport Complete! Shutting down...");
/*STEP 5: Check If Provider Exists(Query endpoint using FirstName, LastName and NPI)*/
/*STEP 5a: If exists, do nothing for Provider and move onto next record*/
/*STEP 5b: If not exists, create the provider*/
/*STEP 6: Check if Insurance Record exists for provider (Query endpoint using Insurance Name to find match in Reference tied to provider*/
/*STEP 6a: If exists, do nothing for Insurance and move onto next record*/
/*STEP 6b: If not exists, check if the Insurance referencesource exists*/
/*STEP 6b1: If not exists, create the Insurance referencesource and create the Reference record*/
