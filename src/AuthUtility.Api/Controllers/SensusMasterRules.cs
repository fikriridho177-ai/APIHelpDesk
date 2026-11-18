using System.Globalization;
using AuthUtility.Api.Sensus;
using AuthUtility.Application.DTOs.Sensus;
using AuthUtility.Application.Interfaces;
using AuthUtility.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthUtility.Api.Controllers;

public partial class SensusMasterController
{
    [HttpPost("jenis/{jenisId:int}/rules")]
    public async Task<IActionResult> CreateRule(int jenisId,SaveSnsRuleRequest body)
    {
        if(!await Can(SnsMenu.MasterJenis,MenuPermission.Update))return Forbid();
        if(!await db.SnsJenis.AnyAsync(k=>k.Id==jenisId))return NotFound();
        NormalizeRule(body);
        var error=await ValidateRule(jenisId,body);if(error!=null)return BadRequest(new{message=error});
        var entity=new SnsHptRule{JenisId=jenisId};ApplyRule(entity,body);db.SnsHptRules.Add(entity);await db.SaveChangesAsync();return Ok(RuleDto(entity));
    }
    [HttpPut("rules/{id:int}")]
    public async Task<IActionResult> UpdateRule(int id,SaveSnsRuleRequest body)
    {
        if(!await Can(SnsMenu.MasterJenis,MenuPermission.Update))return Forbid();
        var entity=await db.SnsHptRules.FindAsync(id);if(entity==null)return NotFound();
        NormalizeRule(body);
        var error=await ValidateRule(entity.JenisId,body);if(error!=null)return BadRequest(new{message=error});
        ApplyRule(entity,body);await db.SaveChangesAsync();return Ok(RuleDto(entity));
    }
    [HttpDelete("rules/{id:int}")]
    public async Task<IActionResult> DisableRule(int id)
    {
        if(!await Can(SnsMenu.MasterJenis,MenuPermission.Update))return Forbid();
        var entity=await db.SnsHptRules.FindAsync(id);if(entity==null)return NotFound();entity.Active=false;await db.SaveChangesAsync();return NoContent();
    }
    private static void NormalizeRule(SaveSnsRuleRequest r)
    {
        static string? Optional(string? value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim();
        r.ParameterCode=(r.ParameterCode??"").Trim().ToUpperInvariant();r.Operator=(r.Operator??"").Trim();
        r.Aggregation=(r.Aggregation??"").Trim().ToUpperInvariant();r.ResultCode=(r.ResultCode??"").Trim().ToUpperInvariant();
        r.Color=(r.Color??"").Trim().ToLowerInvariant();r.ElseColor=(r.ElseColor??"").Trim().ToLowerInvariant();
        r.JenisForm=Optional(r.JenisForm)?.ToUpperInvariant();r.StatusLahan=Optional(r.StatusLahan);
        r.ConditionParameterCode=Optional(r.ConditionParameterCode)?.ToUpperInvariant();r.ConditionOperator=Optional(r.ConditionOperator);r.ConditionValue=Optional(r.ConditionValue);
    }
    private async Task<string?> ValidateRule(int jenisId,SaveSnsRuleRequest r)
    {
        if(!SensusHptEvaluation.Operators.Contains(r.Operator) || !SensusHptEvaluation.Aggregations.Contains(r.Aggregation) ||
            !SensusHptEvaluation.Colors.Contains(r.Color) || !SensusHptEvaluation.Colors.Contains(r.ElseColor) ||
            r.ResultCode is not ("PERLU_PENGENDALIAN" or "MONITORING"))return "Operator, agregasi, hasil atau warna tidak valid.";
        if(string.IsNullOrWhiteSpace(r.ParameterCode)||r.ParameterCode.Length>60||r.ThresholdUnit==null||r.ThresholdUnit.Length>50||
            string.IsNullOrWhiteSpace(r.Message)||r.Message.Length>2000||string.IsNullOrWhiteSpace(r.ElseMessage)||r.ElseMessage.Length>2000||
            r.ThresholdValue>9999999999999.999999m||r.ThresholdValue< -9999999999999.999999m||decimal.Round(r.ThresholdValue,6)!=r.ThresholdValue)return "Parameter, ambang atau pesan rule tidak valid.";
        if(r.JenisForm!=null && r.JenisForm is not ("FORM1" or "FORM2"))return "Jenis form rule tidak valid.";
        if(r.StatusLahan?.Length>50)return "Status lahan terlalu panjang.";
        var parameters=await db.SnsParameters.AsNoTracking().Where(p=>p.JenisId==jenisId&&p.Active).ToListAsync();
        var key=r.ParameterCode.Trim().ToUpperInvariant();
        if(!SensusHptEvaluation.Numbers.Contains(key) && !parameters.Any(p=>p.Code.Equals(key,StringComparison.OrdinalIgnoreCase)&&p.TipeData=="NUMBER"))return "Parameter harus metrik sensus atau NUMBER aktif jenis ini.";
        if(string.IsNullOrWhiteSpace(r.ConditionParameterCode))return string.IsNullOrWhiteSpace(r.ConditionOperator)&&string.IsNullOrWhiteSpace(r.ConditionValue)?null:"Lengkapi atau kosongkan seluruh kondisi.";
        if(r.ConditionParameterCode.Length>60||!SensusHptEvaluation.Operators.Contains(r.ConditionOperator)||string.IsNullOrWhiteSpace(r.ConditionValue)||r.ConditionValue.Length>150)return "Kondisi rule tidak lengkap.";
        key=r.ConditionParameterCode.Trim().ToUpperInvariant();var p=parameters.FirstOrDefault(p=>p.Code.Equals(key,StringComparison.OrdinalIgnoreCase));
        if(SensusHptEvaluation.Numbers.Contains(key)||p?.TipeData=="NUMBER")
        {
            var value=(r.ConditionValue??"").Replace(',','.').Trim();
            if(!decimal.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out _))return "Kondisi numerik harus berisi angka.";
            r.ConditionValue=value;
            return null;
        }
        if(!SensusHptEvaluation.Texts.Contains(key)&&p==null)return "Parameter kondisi tidak aktif/tidak dikenal.";
        if(r.ConditionOperator!="=")return "Kondisi teks menggunakan operator =.";
        if(p?.TipeData=="OPTION"&&!(p.Opsi??"").Split(',').Any(v=>v.Trim().Equals(r.ConditionValue.Trim(),StringComparison.OrdinalIgnoreCase)))return "Nilai kondisi tidak termasuk pilihan master.";
        return null;
    }
    private static void ApplyRule(SnsHptRule r,SaveSnsRuleRequest b)
    {
        r.ParameterCode=b.ParameterCode.Trim().ToUpperInvariant();r.Operator=b.Operator;r.ThresholdValue=b.ThresholdValue;r.ThresholdUnit=b.ThresholdUnit.Trim();r.Aggregation=b.Aggregation;
        r.ConditionParameterCode=string.IsNullOrWhiteSpace(b.ConditionParameterCode)?null:b.ConditionParameterCode.Trim().ToUpperInvariant();
        r.ConditionOperator=r.ConditionParameterCode==null?null:b.ConditionOperator;r.ConditionValue=r.ConditionParameterCode==null?null:b.ConditionValue?.Trim();
        r.StatusLahan=SensusBusinessContext.NormalizeLand(b.StatusLahan);r.JenisForm=b.JenisForm;r.ResultCode=b.ResultCode;r.Message=b.Message.Trim();r.Color=b.Color;
        r.ElseMessage=b.ElseMessage.Trim();r.ElseColor=b.ElseColor;r.SortOrder=b.SortOrder;r.Active=b.Active;
    }
    private static SnsRuleDto RuleDto(SnsHptRule r)=>new(r.Id,r.JenisId,r.ParameterCode,r.Operator,r.ThresholdValue,r.ThresholdUnit,r.Aggregation,
        r.ConditionParameterCode,r.ConditionOperator,r.ConditionValue,r.StatusLahan,r.JenisForm,r.ResultCode,r.Message,r.Color,r.ElseMessage,r.ElseColor,r.Active,r.SortOrder);
}
