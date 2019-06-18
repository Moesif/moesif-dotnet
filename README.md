# Moesif Middleware for DotNet

[![Built For][ico-built-for]][link-built-for]
[![Latest Version][ico-version]][link-package]
[![Software License][ico-license]][link-license]
[![Source Code][ico-source]][link-source]

Middleware to capture _incoming_ API calls from .NET apps and send to the Moesif API Analytics platform.

[Source Code on GitHub](https://github.com/Moesif/moesif-dotnet)

## How to install

Install the Nuget Package:

```bash
Install-Package Moesif.Middleware
```

## How to use

In `Startup.cs` file in your project directory, please add `app.UseMiddleware<MoesifMiddleware>(moesifOptions);` to the pipeline.

Because of middleware execution order, it is best to add middleware __below__ SessionMiddleware
and AuthenticationMiddleware, because they add useful session data that enables deeper error analysis. On the other hand, if you have other middleware that modified response before going out, you may choose to place Moesif middleware __above__ the middleware modifying response. This allows Moesif to see the modifications to the response data and see closer to what is going over the wire.

Add the middleware to your application:

```csharp
app.UseMiddleware<MoesifMiddleware>(moesifOptions);
```

Also, add moesifOptions to your settings file,

```
moesifOptions = {
    'ApplicationId': 'Your Application ID Found in Settings on Moesif',
    ...
    # other options see below.
}
```

You can find your Application Id from [_Moesif Dashboard_](https://www.moesif.com/) -> _Top Right Menu_ -> _App Setup_

## Configuration options

#### __`ApplicationId`__
(__required__), _string_, is obtained via your Moesif Account, this is required.

#### __`Skip`__
(optional) _(HttpRequest, HttpResponse) => boolean_, a function that takes a request and a response, and returns true if you want to skip this particular event.

#### __`IdentifyUser`__
(optional) _(HttpRequest, HttpResponse) => string_, a function that takes a request and a response, and returns a string that is the user id used by your system. While Moesif identify users automatically, if your set up is very different from the standard implementations, it would be helpful to provide this function.

#### __`IdentifyCompany`__
(optional) _(HttpRequest, HttpResponse) => string_, a function that takes a request and a response, and returns a string that is the company id for this event.

#### __`GetSessionToken`__
(optional) _(HttpRequest, HttpResponse) => string_, a function that takes a request and a response, and returns a string that is the session token for this event. Again, Moesif tries to get the session token automatically, but if you setup is very different from standard, this function will be very help for tying events together, and help you replay the events.

#### __`GetMetadata`__
(optional) _(HttpRequest, HttpResponse) => dictionary_, getMetadata is a function that returns an object that allows you
to add custom metadata that will be associated with the event. The metadata must be a dictionary that can be converted to JSON. For example, you may want to save a VM instance_id, a trace_id, or a tenant_id with the request.

#### __`MaskEventModel`__
(optional) _(EventModel) => EventModel_, a function that takes an EventModel and returns an EventModel with desired data removed. Use this if you prefer to write your own mask function. The return value must be a valid EventModel required by Moesif data ingestion API. For details regarding EventModel please see the [Moesif CSharp API Documentation](https://www.moesif.com/docs/api?csharp#).

#### __`ApiVersion`__
(optional), _string_, api version associated with this particular event.

#### __`LocalDebug`__
_boolean_, set to True to print internal log messages for debugging SDK integration issues.

#### __`Capture_Outgoing_Requests`__
(optional), Set to capture all outgoing API calls from your app to third parties like Stripe or to your own dependencies while using [System.Net.Http](https://docs.microsoft.com/en-us/dotnet/api/system.net.http?view=netframework-4.8) package. The options below is applied to outgoing API calls. When the request is outgoing, for options functions that take request and response as input arguments, the request and response objects passed in are [HttpRequestMessage](https://docs.microsoft.com/en-us/uwp/api/windows.web.http.httprequestmessage) request and [HttpResponseMessage](https://docs.microsoft.com/en-us/uwp/api/windows.web.http.httpresponsemessage) response objects.

How to configure your application to start capturing outgoing API calls.

```csharp
using System.Net.Http;
using Moesif.Middleware.Helpers;

// moesifOptions is an object of type Dictionary<string, object> which holds configuration options for your application.
MoesifCaptureOutgoingRequestHandler handler = new MoesifCaptureOutgoingRequestHandler(new HttpClientHandler(), moesifOptions);
HttpClient client = new HttpClient(handler);
```

##### __`GetMetadataOutgoing`__
(optional) _(HttpRequestMessage, HttpResponseMessage) => dictionary_, getMetadata is a function that returns an object that allows you
to add custom metadata that will be associated with the event. The metadata must be a dictionary that can be converted to JSON. For example, you may want to save a VM instance_id, a trace_id, or a tenant_id with the request.

##### __`GetSessionTokenOutgoing`__
(optional) _(HttpRequestMessage, HttpResponseMessage) => string_, a function that takes a HttpRequestMessage and a HttpResponseMessage, and returns a string that is the session token for this event. Again, Moesif tries to get the session token automatically, but if you setup is very different from standard, this function will be very help for tying events together, and help you replay the events.

##### __`IdentifyUserOutgoing`__
(optional) _(HttpRequestMessage, HttpResponseMessage) => string_, a function that takes a HttpRequestMessage and a HttpResponseMessage, and returns a string that is the user id used by your system. While Moesif identify users automatically, if your set up is very different from the standard implementations, it would be helpful to provide this function.

##### __`IdentifyCompanyOutgoing`__
(optional) _(HttpRequestMessage, HttpResponseMessage) => string_, a function that takes a HttpRequestMessage and a HttpResponseMessage, and returns a string that is the company id for this event.

##### __`SkipOutgoing`__
(optional) _(HttpRequestMessage, HttpResponseMessage) => boolean_, a function that takes a HttpRequestMessage and a HttpResponseMessage, and returns true if you want to skip this particular event.

##### __`MaskEventModelOutgoing`__
(optional) _(EventModel) => EventModel_, a function that takes an EventModel and returns an EventModel with desired data removed. Use this if you prefer to write your own mask function. The return value must be a valid EventModel required by Moesif data ingestion API. For details regarding EventModel please see the [Moesif CSharp API Documentation](https://www.moesif.com/docs/api?csharp#).

### Example:

```csharp
public static Func<HttpRequest, HttpResponse, string> IdentifyUser = (HttpRequest req, HttpResponse res) =>  {
    return "my_user_id";  
} ;

public static Func<HttpRequest, HttpResponse, string> IdentifyCompany = (HttpRequest req, HttpResponse res) =>  {
    return "12345";  
} ;

public static Func<HttpRequest, HttpResponse, string> GetSessionToken = (HttpRequest req, HttpResponse res) => {
    return "23jdf0owekfmcn4u3qypxg09w4d8ayrcdx8nu2ng]s98y18cx98q3yhwmnhcfx43f";
};

public static Func<HttpRequest, HttpResponse, Dictionary<string, string>> GetMetadata = (HttpRequest req, HttpResponse res) => {
    Dictionary<string, string> metadata = new Dictionary<string, string>
    {
        { "email", "abc@email.com" },
        { "name", "abcdef" },
        { "image", "123" }
    };
    return metadata;
};

public static Func<HttpRequest, HttpResponse, bool> Skip = (HttpRequest req, HttpResponse res) => {
    string uri = new Uri(req.GetDisplayUrl()).ToString();
    if (uri.Contains("test"))
    {
        return true;
    }
    return false;
};

public static Func<EventModel, EventModel> MaskEventModel = (EventModel event_model) => {
    event_model.UserId = "masked_user_id";
    return event_model;
};

static public Dictionary<string, object> moesifOptions = new Dictionary<string, object>
{
    {"ApplicationId", "Your Application ID Found in Settings on Moesif"},
    {"LocalDebug", true},
    {"ApiVersion", "1.0.0"},
    {"IdentifyUser", IdentifyUser},
    {"IdentifyCompany", IdentifyCompany},
    {"GetSessionToken", GetSessionToken},
    {"GetMetadata", GetMetadata},
    {"Skip", Skip},
    {"MaskEventModel", MaskEventModel}
};

```
## Update User

### UpdateUser method
A method is attached to the moesif middleware object to update the users profile or metadata.
The metadata field can be any custom data you want to set on the user. The `user_id` field is required.

```csharp
Dictionary<string, object> metadata = new Dictionary<string, object>
{
    {"email", "johndoe@acmeinc.com"},
    {"string_field", "value_1"},
    {"number_field", 0},
    {"object_field", new Dictionary<string, string> {
        {"field_a", "value_a"},
        {"field_b", "value_b"}
        }
    }
};

Dictionary<string, object> user = new Dictionary<string, object>
{
    {"user_id", "csharpapiuser"},
    {"metadata", metadata},
};

MoesifMiddleware moesifMiddleware = new MoesifMiddleware(RequestDelegate next, Dictionary<string, object> moesifOptions)
moesifMiddleware.UpdateUser(user);
```

### UpdateUsersBatch method
A method is attached to the moesif middleware object to update the users profile or metadata in batch.
The metadata field can be any custom data you want to set on the user. The `user_id` field is required.

```csharp
List<Dictionary<string, object>> usersBatch = new List<Dictionary<string, object>>();
Dictionary<string, object> metadata = new Dictionary<string, object>
{
    {"email", "johndoe@acmeinc.com"},
    {"string_field", "value_1"},
    {"number_field", 0},
    {"object_field", new Dictionary<string, string> {
        {"field_a", "value_a"},
        {"field_b", "value_b"}
        }
    }
};

Dictionary<string, object> userA = new Dictionary<string, object>
{
    {"user_id", "csharpapiuser"},
    {"metadata", metadata},
};

Dictionary<string, object> userB = new Dictionary<string, object>
{
    {"user_id", "csharpapiuser1"},
    {"modified_time", DateTime.UtcNow},
    {"metadata", metadata},
};

usersBatch.Add(userA);
usersBatch.Add(userB);

MoesifMiddleware moesifMiddleware = new MoesifMiddleware(RequestDelegate next, Dictionary<string, object> moesifOptions)
moesifMiddleware.UpdateUsersBatch(usersBatch);
```

## Update Company

### UpdateCompany method
A method is attached to the moesif middleware object to update the company profile or metadata.
The metadata field can be any custom data you want to set on the company. The `company_id` field is required.

```csharp
Dictionary<string, object> metadata = new Dictionary<string, object>
{
    {"email", "johndoe@acmeinc.com"},
    {"string_field", "value_1"},
    {"number_field", 0},
    {"object_field", new Dictionary<string, string> {
        {"field_a", "value_a"},
        {"field_b", "value_b"}
        }
    }
};

Dictionary<string, object> company = new Dictionary<string, object>
{
    {"company_id", "csharpapicompany"},
    {"company_domain", "acmeinc.com"},
    {"metadata", metadata},
};

MoesifMiddleware moesifMiddleware = new MoesifMiddleware(RequestDelegate next, Dictionary<string, object> moesifOptions)
moesifMiddleware.UpdateCompany(company);
```

### UpdateCompaniesBatch method
A method is attached to the moesif middleware object to update the companies profile or metadata in batch.
The metadata field can be any custom data you want to set on the company. The `company_id` field is required.

```csharp
List<Dictionary<string, object>> companiesBatch = new List<Dictionary<string, object>>();
Dictionary<string, object> metadata = new Dictionary<string, object>
{
    {"email", "johndoe@acmeinc.com"},
    {"string_field", "value_1"},
    {"number_field", 0},
    {"object_field", new Dictionary<string, string> {
        {"field_a", "value_a"},
        {"field_b", "value_b"}
        }
    }
};

Dictionary<string, object> companyA = new Dictionary<string, object>
{
    {"user_id", "csharpapicompany"},
    {"company_domain", "acmeinc.com"},
    {"metadata", metadata},
};

Dictionary<string, object> companyB = new Dictionary<string, object>
{
    {"user_id", "csharpapicompany1"},
    {"company_domain", "nowhere.com"},
    {"metadata", metadata},
};

companiesBatch.Add(companyA);
companiesBatch.Add(companyB);

MoesifMiddleware moesifMiddleware = new MoesifMiddleware(RequestDelegate next, Dictionary<string, object> moesifOptions)
moesifMiddleware.UpdateCompaniesBatch(companiesBatch);
```

## How to test

1. Manually clone the git repo
2. From terminal/cmd navigate to the root directory of the middleware.
3. Invoke 'Install-Package Moesif.Middleware'
4. Add your own application id to 'Moesif.Middleware.Test/MoesifMiddlewareTest.cs'. You can find your Application Id from [_Moesif Dashboard_](https://www.moesif.com/) -> _Top Right Menu_ -> _Installation_
5. The tests are contained in the Moesif.Middleware.Test project. In order to invoke these test cases, you will need NUnit 3.0 Test Adapter Extension for Visual Studio. Once the SDK is complied, the test cases should appear in the Test Explorer window. Here, you can click Run All to execute these test cases.

## Example

An example Moesif integration based on quick start tutorial:
[Moesif .NET Example](https://github.com/Moesif/moesif-dotnet-example)

## Other integrations

To view more more documentation on integration options, please visit __[the Integration Options Documentation](https://www.moesif.com/docs/getting-started/integration-options/).__

[ico-built-for]: https://img.shields.io/badge/built%20for-dotnet-blue.svg
[ico-version]: https://img.shields.io/nuget/v/Moesif.Middleware.svg
[ico-downloads]: https://img.shields.io/nuget/dt/Moesif.Middleware.svg
[ico-license]: https://img.shields.io/badge/License-Apache%202.0-green.svg
[ico-source]: https://img.shields.io/github/last-commit/moesif/moesifdjango.svg?style=social

[link-built-for]: https://www.microsoft.com/net
[link-package]: https://www.nuget.org/packages/Moesif.Middleware
[link-downloads]: https://www.nuget.org/packages/Moesif.Middleware
[link-license]: https://raw.githubusercontent.com/Moesif/moesif-dotnet/master/LICENSE
[link-source]: https://github.com/Moesif/moesif-dotnet
