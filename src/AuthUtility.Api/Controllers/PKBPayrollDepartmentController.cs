using AuthUtility.Application.Interfaces;
using AuthUtility.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace AuthUtility.Api.Controllers;

[ApiController]
[Route("api/utility/pkb-payroll/departments")]
[Authorize]
public class PKBPayrollDepartmentController(
    PKBPayrollDbContext pkbDb,
    IPermissionService permissionService,
    ILogger<PKBPayrollDepartmentController> logger
) : ControllerBase
{
    private const string MenuCode = "MASTER_USER";

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        if (!await permissionService.HasPermissionAsync(User, MenuCode, MenuPermission.Read)) return Forbid();

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
                    DeptID,
                    DeptAbbr,
                    Departemen,
                    CompanyID,
                    ShortName,
                    NotActive
                FROM vwMstDept
                WHERE NotActive = 0
                ORDER BY Departemen, ShortName, DeptID
            ";

            await using var reader = await command.ExecuteReaderAsync();
            var result = new List<object>();

            while (await reader.ReadAsync())
            {
                result.Add(new
                {
                    id = reader["DeptID"] == DBNull.Value
                        ? string.Empty
                        : reader["DeptID"].ToString()!.Trim(),

                    code = reader["DeptAbbr"] == DBNull.Value
                        ? string.Empty
                        : reader["DeptAbbr"].ToString()!.Trim(),

                    name = reader["Departemen"] == DBNull.Value
                        ? string.Empty
                        : reader["Departemen"].ToString()!.Trim(),

                    companyId = reader["CompanyID"] == DBNull.Value
                        ? string.Empty
                        : reader["CompanyID"].ToString()!.Trim(),

                    shortName = reader["ShortName"] == DBNull.Value
                        ? string.Empty
                        : reader["ShortName"].ToString()!.Trim()
                });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Gagal mengambil data departemen PKBPayroll.");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new
                {
                    message = "Gagal mengambil data departemen PKBPayroll."
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
