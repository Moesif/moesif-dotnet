# Moesif Middleware for .NET

Middleware to capture _incoming_ API calls and send to the Moesif API Analytics platform.

[Source Code on GitHub](https://github.com/Moesif/moesif-dotnet)

[Nuget Package](https://www.nuget.org/packages/Moesif.Middleware/)

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

### Example:

```csharp
public static Func<HttpRequest, HttpResponse, string> IdentifyUser = (HttpRequest req, HttpResponse res) =>  {
    return "my_user_id";  
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
    {"GetSessionToken", GetSessionToken},
    {"GetMetadata", GetMetadata},
    {"Skip", Skip},
    {"MaskEventModel", MaskEventModel}
};

```

## Example

An example Moesif integration based on quick start tutorial:
[Moesif CSharp Example](https://github.com/Moesif/moesif-dotnet-example)

## Other integrations

To view more more documentation on integration options, please visit __[the Integration Options Documentation](https://www.moesif.com/docs/getting-started/integration-options/).__
