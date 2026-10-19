using AuthUtility.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace AuthUtility.Api.Controllers;

[ApiController]
[Route("api/utility/pkb-payroll/employees")]
[Authorize]
public class PKBPayrollEmployeeController(
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
                    RegNo,
                    NAMA,
                    NIK,
                    CompanyID,
                    DeptID
                FROM tblMstKaryawan
                WHERE TGLKELUAR IS NULL
                ORDER BY NAMA, RegNo
            ";

            await using var reader = await command.ExecuteReaderAsync();
            var result = new List<object>();

            while (await reader.ReadAsync())
            {
                result.Add(new
                {
                    regNo = reader["RegNo"] == DBNull.Value
                        ? (long?)null
                        : Convert.ToInt64(reader["RegNo"]),

                    name = reader["NAMA"] == DBNull.Value
                        ? string.Empty
                        : reader["NAMA"].ToString()!.Trim(),

                    nik = reader["NIK"] == DBNull.Value
                        ? string.Empty
                        : reader["NIK"].ToString()!.Trim(),

                    companyId = reader["CompanyID"] == DBNull.Value
                        ? (int?)null
                        : Convert.ToInt32(reader["CompanyID"]),

                    deptId = reader["DeptID"] == DBNull.Value
                        ? string.Empty
                        : reader["DeptID"].ToString()!.Trim()
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
                    message = "Gagal mengambil data karyawan PKBPayroll.",
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
