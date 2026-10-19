using AuthUtility.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace AuthUtility.Api.Controllers;

[ApiController]
[Route("api/utility/pkb-payroll/companies")]
[Authorize]
public class PKBPayrollCompanyController(
    PKBPayrollDbContext pkbDb
) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var connection = pkbDb.Database.GetDbConnection();

        try
        {
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            await using var command = connection.CreateCommand();

            command.CommandText = @"
                SELECT
                    Kode,
                    Nama,
                    ShortName,
                    NotActive
                FROM tblMstPerusahaan
                WHERE NotActive = 0
                ORDER BY Nama
            ";

            await using var reader = await command.ExecuteReaderAsync();
            var result = new List<object>();

            while (await reader.ReadAsync())
            {
                var code = reader["Kode"] == DBNull.Value
                    ? string.Empty
                    : reader["Kode"].ToString()!.Trim();

                result.Add(new
                {
                    id = code,
                    code,
                    name = reader["Nama"] == DBNull.Value
                        ? string.Empty
                        : reader["Nama"].ToString()!.Trim(),
                    shortName = reader["ShortName"] == DBNull.Value
                        ? string.Empty
                        : reader["ShortName"].ToString()!.Trim()
                });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    message = "Gagal mengambil data perusahaan PKBPayroll.",
                    detail = ex.Message
                }
            );
        }
        finally
        {
            if (connection.State == ConnectionState.Open)
            {
                await connection.CloseAsync();
            }
        }
    }
}
