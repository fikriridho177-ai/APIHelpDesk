using System.Text.Json;
using AuthUtility.Application.DTOs.Sensus;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace AuthUtility.Tests;

public class SensusRuleRequestTests
{
    [Theory]
    [InlineData("%")]
    [InlineData("")]
    public void FrontendRulePayloadPassesMvcValidation(string unit)
    {
        var payload=JsonSerializer.Serialize(new{parameterCode="PERSEN_TERSERANG",@operator=">=",thresholdValue=5,thresholdUnit=unit,aggregation="AVG",resultCode="PERLU_PENGENDALIAN",message="Perlu pengendalian.",color="red",elseMessage="Monitoring berkala.",elseColor="green",active=true,sortOrder=0});
        var body=JsonSerializer.Deserialize<SaveSnsRuleRequest>(payload,new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var services=new ServiceCollection();services.AddLogging();services.AddControllers();using var provider=services.BuildServiceProvider();
        var context=new ActionContext{HttpContext=new DefaultHttpContext{RequestServices=provider}};
        provider.GetRequiredService<IObjectModelValidator>().Validate(context,null,"",body);
        Assert.True(context.ModelState.IsValid,string.Join("; ",context.ModelState.SelectMany(p=>p.Value!.Errors.Select(e=>p.Key+": "+e.ErrorMessage))));
    }
}
