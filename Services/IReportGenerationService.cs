using RekovaBE_CSharp.Models.DTOs;

namespace RekovaBE_CSharp.Services
{
    public interface IReportGenerationService
    {
        byte[] GenerateExcelReport(ReportDataDto data, string reportType);
        Task<byte[]> GeneratePdfReport(ReportDataDto data, string reportType);
    }
}