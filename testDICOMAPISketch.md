The DICOM Web API we will be using has the ability to test whether it is up and running and whether our API key is successful. Here is an example of how to use it. 
```cs
                    var ipAddress = _ss.AppSettings.DICOMAnonIPAddress;
                    var apiKey = _ss.AppSettings.DICOMAnonAPIKey;
                    var client = new DAClient(ipAddress, DAClientSyncService.API_PORT, apiKey);
                    var success = client.IsClientConnected();
```

We will need to be able to store settings in this application, specifically:
- IP address
- API key
- Port
that will be used to communicate with this service. Take a look at the Models app settings class to see the basic structure that needs to be stored. 

Each of these settings needs to be able to be adjusted in the console application. Please use the Spectre console to enable the selection and ability to modify these settings as well as the storage of these settings in a local directory, probably like settings.json. The settings screen in the console application also needs to be able to test the connection to the DICOMAnon API, and it needs to indicate to the user whether or not the connection is successful

