using System.Text.Json;
using AuthUtility.Application.DTOs.Sensus;
using AuthUtility.Domain.Entities;

namespace AuthUtility.Api.Sensus;

public static class SensusParcelHpt
{
    public static (string Tingkat, decimal? Rata) Level(SnsHeader header)
    {
        var indicators=header.Details.Where(d=>d.Parameter?.IsIndikator==true && d.NilaiAngka!=null)
            .GroupBy(d=>d.ParameterId).OrderBy(g=>g.Key)
            .Select(g=>new {Rata=g.Average(d=>d.NilaiAngka),Parameter=g.First().Parameter!}).ToList();
        var evaluation=SensusHptEvaluation.Evaluate(header);
        var attacked=header.HptDetails.Count>0?header.HptDetails.Max(d=>d.JumlahSerangan):header.JumlahPokokTerserang;
        var level=evaluation.PerluPengendalian==true?SnsTingkat.Berat:
            evaluation.PerluPengendalian==false?(attacked==0?SnsTingkat.Nihil:SnsTingkat.Ringan):
            SnsTingkat.Max(indicators.Select(i=>SnsTingkat.Hitung(i.Rata,i.Parameter.AmbangSedang,i.Parameter.AmbangBerat)));
        return(level,indicators.FirstOrDefault()?.Rata);
    }

    public static decimal? Percentage(int attacked, int samples) => samples > 0 ? decimal.Round(attacked * 100m / samples, 4) : null;

    public static string? ValidateInputs(IReadOnlyCollection<SnsHptInput> inputs, IReadOnlyCollection<SnsJenis> kinds,
        int samples, IEnumerable<int>? historicalIds = null, string form = "FORM1")
    {
        if (samples <= 0) return "Jumlah pohon sensus harus lebih dari nol.";
        if (inputs.Count == 0) return "Isi seluruh HPT pada persil.";
        if (inputs.Select(x => x.HptId).Distinct().Count() != inputs.Count) return "HPT tidak boleh diisi berulang.";
        if (inputs.Any(x => x.JumlahSerangan < 0 || x.JumlahSerangan > samples)) return "Jumlah serangan setiap HPT harus antara nol dan jumlah pohon sensus.";
        var historical = (historicalIds ?? Array.Empty<int>()).ToHashSet();
        var expected = kinds.Where(k => k.Active && (form != "FORM2" || k.Form2Enabled) || historical.Contains(k.Id)).Select(k => k.Id).ToHashSet();
        if (!expected.SetEquals(inputs.Select(x => x.HptId))) return "Daftar HPT berubah atau belum lengkap. Muat ulang form dan isi seluruh HPT.";
        return null;
    }

    // Produce an observation view without mutating the tracked parcel header or its child collections.
    public static SnsHeader ForHpt(SnsHeader h, SnsHptDetail d) => new()
    {
        Id=h.Id,SensusNo=h.SensusNo,ClientUuid=h.ClientUuid,JenisId=d.HptId,Jenis=d.Hpt,
        PersilSumber=h.PersilSumber,PersilId=h.PersilId,PersilKode=h.PersilKode,KodeWilayah=h.KodeWilayah,KodeAfdeling=h.KodeAfdeling,
        Komoditas=h.Komoditas,BlokId=h.BlokId,Blok=h.Blok,TanggalSensus=h.TanggalSensus,PetugasUserId=h.PetugasUserId,Petugas=h.Petugas,
        JumlahPokokSampel=h.JumlahPokokSampel,JumlahPokokTerserang=d.JumlahSerangan,Latitude=h.Latitude,Longitude=h.Longitude,
        Catatan=h.Catatan,DeviceInfo=h.DeviceInfo,Status=h.Status,SubmittedAt=h.SubmittedAt,ApprovedByUserId=h.ApprovedByUserId,
        ApprovedBy=h.ApprovedBy,ApprovedAt=h.ApprovedAt,ApprovalNote=h.ApprovalNote,CreatedAt=h.CreatedAt,UpdatedAt=h.UpdatedAt,
        JenisForm=h.JenisForm,StatusLahan=h.StatusLahan,StatusLahanSumber=h.StatusLahanSumber,FaseTanaman=h.FaseTanaman,LuasHa=h.LuasHa,
        JumlahPokokPersil=h.JumlahPokokPersil,BulanTanam=h.BulanTanam,JumlahJalur=h.JumlahJalur,PohonMatiPetir=h.PohonMatiPetir,
        PohonMatiHpt=h.PohonMatiHpt,PohonMatiLain=h.PohonMatiLain,Pengendalian=h.Pengendalian,MusuhAlami=h.MusuhAlami,Cuaca=h.Cuaca,
        Details=h.Details.Where(x => x.Parameter?.JenisId == d.HptId || d.Hpt?.Parameters.Any(p => p.Id == x.ParameterId) == true).ToList(),
        Fotos=h.Fotos.ToList()
    };

    public static IEnumerable<SnsHeader> Expand(SnsHeader header) => header.HptDetails.Count > 0
        ? header.HptDetails.Select(d => ForHpt(header,d))
        : header.JenisId.HasValue ? new[] { header } : Array.Empty<SnsHeader>();

    public static SnsEvaluationDto Snapshot(SnsHptDetail d)
    {
        if (!string.IsNullOrWhiteSpace(d.HasilEvaluasi))
        {
            try { if (JsonSerializer.Deserialize<SnsEvaluationDto>(d.HasilEvaluasi) is { } result) return result; }
            catch (JsonException) { /* Older observations may have no structured snapshot. */ }
        }
        return new("NONE",null,null,null,"Snapshot evaluasi historis belum tersedia.",Color:"gray");
    }

    public static List<SnsHptDetailDto> Dtos(SnsHeader h) => h.HptDetails.Count > 0
        ? h.HptDetails.OrderBy(x=>x.Hpt?.ReportGroup).ThenBy(x=>x.Hpt?.Name).Select(d=>new SnsHptDetailDto(d.HptId,d.Hpt?.Name??"",d.Hpt?.ReportGroup??"LAINNYA",d.JumlahSerangan,Percentage(d.JumlahSerangan,h.JumlahPokokSampel),Snapshot(d),SensusHptEvaluation.Evaluate(ForHpt(h,d)),Level(ForHpt(h,d)).Tingkat)).ToList()
        : h.JenisId is int id ? new(){new(id,h.Jenis?.Name??"",h.Jenis?.ReportGroup??"LAINNYA",h.JumlahPokokTerserang,Percentage(h.JumlahPokokTerserang,h.JumlahPokokSampel),new("NONE",null,null,null,"Snapshot evaluasi historis belum tersedia.",Color:"gray"),SensusHptEvaluation.Evaluate(h),Level(h).Tingkat)} : new();
}
