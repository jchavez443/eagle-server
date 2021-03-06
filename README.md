# eagle-server

https://github.com/jchavez-bw/eagle-server

C# Lightweight  server


## Version 2.0.0

Path parameter support was added in version 2.0.0 and the request object now has a different form of 

```csharp
public class EagleRequest
{
    public PathInfo PathInfo { get; set; }
    
    public NameValueCollection QueryParams { get; set; }

    public dynamic Body { get; set; }

    public HttpListenerRequest RawRequest { get; set; }
}
```

with `PathInfo` defined as 

```csharp
public class PathInfo
{
    public dynamic PathParameters { get; set; }

    public string RawPath { get; set; }

    public string VariablePath { get; set; }
}
```


## Suggested Import (using static)
```csharp
using static Eagle.Server;
```

## Starting the server
The Server must be started before any mappings can be setup
```csharp
port("8181"); //default port is 8080
useHttp(true); //default is false, will use HTTPS
startServerInstance();
```

## Setting up a request path
This will throw a `ServerNotStartedException` if the server has not been started yet.
```csharp
post("/incoming/request", (request, response) => {
    
    //Logic here
    
    return foo(); //Return a string
}
```
*Note: if no error is thrown and "return" is hit then the http status code will be 200 

### Path Paramaters ( version > 2.0.0 )
```csharp
post("/account/{accountId}", (request, response) => {

    var pathParameters = request.PathInfo.PathParameters;
    var body = request.Body;

    // this is for JSON body only.  requst body was { "test" : "value here" }
    string test = body.test;

    //Dynamic object accountId must match exactly the string between the {} ex: {accountId} 
    string accountId = pathParameters.accountId;

    WriteLine($"accountId = {accountId} and test = {test}");

    return "";
});
```

## Supported Methods
* POST
* GET
* PUT
* DELETE

## Non-200 status codes
```csharp
post("/incoming/request", (request, response) => {
    
    throw new HttpStatusAwareException(500, "internal server error"); //Must be HttpStatusAwareException or extension of.
    
    return foo(); //Return a string
}
```
The status code in the exception and the message in the exception will be used as the response status code and the response body.
The HttpStatusAwareException is an exception actively caught and handled. 
If an uncaught exception is thrown within a reply function, the server will catch it and convert it to `new HttpStatusAwareException(500, "internal server error")`

## Headers & etc...
```csharp
post("/incoming/request", (request, response) => {
    
    response.AddHeader("header", "value");
    
    return foo(); //Return a string
}
```
Manipulate the response object for advanced features.

