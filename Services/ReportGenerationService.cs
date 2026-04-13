using OfficeOpenXml;
using OfficeOpenXml.Style;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RekovaBE_CSharp.Models.DTOs;
using System.Drawing;

namespace RekovaBE_CSharp.Services
{
    public class ReportGenerationService : IReportGenerationService
    {
        private readonly ILogger<ReportGenerationService> _logger;

        public ReportGenerationService(ILogger<ReportGenerationService> logger)
        {
            _logger = logger;
            // EPPlus license configuration
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            // QuestPDF license configuration
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public byte[] GenerateExcelReport(ReportDataDto data, string reportType)
        {
            using var package = new ExcelPackage();
            
            // Executive Summary Sheet
            var summarySheet = package.Workbook.Worksheets.Add("Executive Summary");
            
            // Header
            summarySheet.Cells["A1"].Value = $"{reportType.ToUpper()} REPORT";
            summarySheet.Cells["A1"].Style.Font.Size = 16;
            summarySheet.Cells["A1"].Style.Font.Bold = true;
            summarySheet.Cells["A1"].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(58, 179, 229));
            
            summarySheet.Cells["A2"].Value = $"Generated: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
            summarySheet.Cells["A2"].Style.Font.Size = 10;
            summarySheet.Cells["A2"].Style.Font.Italic = true;
            
            // Officer Details
            summarySheet.Cells["A4"].Value = "OFFICER DETAILS";
            summarySheet.Cells["A4"].Style.Font.Bold = true;
            summarySheet.Cells["A4"].Style.Fill.PatternType = ExcelFillStyle.Solid;
            summarySheet.Cells["A4"].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(58, 179, 229));
            summarySheet.Cells["A4"].Style.Font.Color.SetColor(System.Drawing.Color.White);
            
            summarySheet.Cells["A5"].Value = "Name:";
            summarySheet.Cells["A5"].Style.Font.Bold = true;
            summarySheet.Cells["B5"].Value = data.OfficerName;
            
            summarySheet.Cells["A6"].Value = "Email:";
            summarySheet.Cells["A6"].Style.Font.Bold = true;
            summarySheet.Cells["B6"].Value = data.OfficerEmail;
            
            summarySheet.Cells["A7"].Value = "Role:";
            summarySheet.Cells["A7"].Style.Font.Bold = true;
            summarySheet.Cells["B7"].Value = data.OfficerRole;
            
            // Performance Summary
            summarySheet.Cells["D4"].Value = "PERFORMANCE SUMMARY";
            summarySheet.Cells["D4"].Style.Font.Bold = true;
            summarySheet.Cells["D4"].Style.Fill.PatternType = ExcelFillStyle.Solid;
            summarySheet.Cells["D4"].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(58, 179, 229));
            summarySheet.Cells["D4"].Style.Font.Color.SetColor(System.Drawing.Color.White);
            
            summarySheet.Cells["D5"].Value = "Collection Rate:";
            summarySheet.Cells["D5"].Style.Font.Bold = true;
            summarySheet.Cells["E5"].Value = $"{data.CollectionRate}%";
            
            summarySheet.Cells["D6"].Value = "Success Rate:";
            summarySheet.Cells["D6"].Style.Font.Bold = true;
            summarySheet.Cells["E6"].Value = $"{data.SuccessRate}%";
            
            summarySheet.Cells["D7"].Value = "Total Collected:";
            summarySheet.Cells["D7"].Style.Font.Bold = true;
            summarySheet.Cells["E7"].Value = $"KES {data.TotalCollected:N0}";
            
            // Key Metrics Table
            summarySheet.Cells["A9"].Value = "KEY METRICS";
            summarySheet.Cells["A9"].Style.Font.Bold = true;
            summarySheet.Cells["A9"].Style.Fill.PatternType = ExcelFillStyle.Solid;
            summarySheet.Cells["A9"].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(64, 50, 46));
            summarySheet.Cells["A9"].Style.Font.Color.SetColor(System.Drawing.Color.White);
            
            var metrics = new[]
            {
                new { Metric = "Total Customers", Value = data.TotalCustomers.ToString(), Target = "15" },
                new { Metric = "Active Customers", Value = data.ActiveCustomers.ToString(), Target = "12" },
                new { Metric = "Overdue Customers", Value = data.OverdueCustomers.ToString(), Target = "0" },
                new { Metric = "Total Collections", Value = $"KES {data.TotalCollected:N0}", Target = "KES 400,000" },
                new { Metric = "Monthly Collections", Value = $"KES {data.MonthlyCollected:N0}", Target = "KES 150,000" },
                new { Metric = "Weekly Collections", Value = $"KES {data.WeeklyCollected:N0}", Target = "KES 40,000" }
            };
            
            int row = 10;
            summarySheet.Cells[$"A{row}"].Value = "Metric";
            summarySheet.Cells[$"B{row}"].Value = "Value";
            summarySheet.Cells[$"C{row}"].Value = "Target";
            summarySheet.Cells[$"A{row}:C{row}"].Style.Font.Bold = true;
            summarySheet.Cells[$"A{row}:C{row}"].Style.Fill.PatternType = ExcelFillStyle.Solid;
            summarySheet.Cells[$"A{row}:C{row}"].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightGray);
            
            foreach (var metric in metrics)
            {
                row++;
                summarySheet.Cells[$"A{row}"].Value = metric.Metric;
                summarySheet.Cells[$"B{row}"].Value = metric.Value;
                summarySheet.Cells[$"C{row}"].Value = metric.Target;
            }
            
            // Auto-fit columns
            summarySheet.Cells.AutoFitColumns();
            
            // Collections Details Sheet
            if (data.Transactions != null && data.Transactions.Any())
            {
                var detailsSheet = package.Workbook.Worksheets.Add("Collections Details");
                
                detailsSheet.Cells["A1"].Value = "COLLECTIONS TRANSACTIONS";
                detailsSheet.Cells["A1"].Style.Font.Bold = true;
                detailsSheet.Cells["A1"].Style.Font.Size = 14;
                
                detailsSheet.Cells["A2"].Value = $"Generated: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
                detailsSheet.Cells["A2"].Style.Font.Italic = true;
                
                // Headers
                var headers = new[] { "Date", "Transaction ID", "Customer Name", "Phone Number", "Amount (KES)", "Status", "Receipt" };
                for (int i = 0; i < headers.Length; i++)
                {
                    detailsSheet.Cells[4, i + 1].Value = headers[i];
                    detailsSheet.Cells[4, i + 1].Style.Font.Bold = true;
                    detailsSheet.Cells[4, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    detailsSheet.Cells[4, i + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(58, 179, 229));
                    detailsSheet.Cells[4, i + 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
                }
                
                int dataRow = 5;
                foreach (var transaction in data.Transactions)
                {
                    detailsSheet.Cells[dataRow, 1].Value = transaction.Date;
                    detailsSheet.Cells[dataRow, 2].Value = transaction.TransactionId;
                    detailsSheet.Cells[dataRow, 3].Value = transaction.CustomerName;
                    detailsSheet.Cells[dataRow, 4].Value = transaction.PhoneNumber;
                    detailsSheet.Cells[dataRow, 5].Value = transaction.Amount;
                    detailsSheet.Cells[dataRow, 5].Style.Numberformat.Format = "#,##0";
                    detailsSheet.Cells[dataRow, 6].Value = transaction.Status;
                    detailsSheet.Cells[dataRow, 7].Value = transaction.Receipt;
                    dataRow++;
                }
                
                detailsSheet.Cells.AutoFitColumns();
            }
            
            // Assigned Customers Sheet
            if (data.Customers != null && data.Customers.Any())
            {
                var customersSheet = package.Workbook.Worksheets.Add("Assigned Customers");
                
                customersSheet.Cells["A1"].Value = "ASSIGNED CUSTOMERS PORTFOLIO";
                customersSheet.Cells["A1"].Style.Font.Bold = true;
                customersSheet.Cells["A1"].Style.Font.Size = 14;
                
                customersSheet.Cells["A2"].Value = $"Generated: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
                customersSheet.Cells["A2"].Style.Font.Italic = true;
                
                // Headers
                var customerHeaders = new[] { "Customer Name", "Phone Number", "Loan Type", "Loan Amount", "Arrears", "Status", "Last Contact" };
                for (int i = 0; i < customerHeaders.Length; i++)
                {
                    customersSheet.Cells[4, i + 1].Value = customerHeaders[i];
                    customersSheet.Cells[4, i + 1].Style.Font.Bold = true;
                    customersSheet.Cells[4, i + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    customersSheet.Cells[4, i + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(58, 179, 229));
                    customersSheet.Cells[4, i + 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
                }
                
                int customerRow = 5;
                foreach (var customer in data.Customers)
                {
                    customersSheet.Cells[customerRow, 1].Value = customer.Name;
                    customersSheet.Cells[customerRow, 2].Value = customer.PhoneNumber;
                    customersSheet.Cells[customerRow, 3].Value = customer.LoanType;
                    customersSheet.Cells[customerRow, 4].Value = customer.LoanAmount;
                    customersSheet.Cells[customerRow, 4].Style.Numberformat.Format = "#,##0";
                    customersSheet.Cells[customerRow, 5].Value = customer.Arrears;
                    customersSheet.Cells[customerRow, 5].Style.Numberformat.Format = "#,##0";
                    customersSheet.Cells[customerRow, 6].Value = customer.Status;
                    customersSheet.Cells[customerRow, 7].Value = customer.LastContact;
                    customerRow++;
                }
                
                // Summary section
                customerRow += 2;
                customersSheet.Cells[$"A{customerRow}"].Value = "PORTFOLIO SUMMARY";
                customersSheet.Cells[$"A{customerRow}"].Style.Font.Bold = true;
                customersSheet.Cells[$"A{customerRow}"].Style.Font.Size = 12;
                
                customerRow++;
                customersSheet.Cells[$"A{customerRow}"].Value = "Total Customers:";
                customersSheet.Cells[$"A{customerRow}"].Style.Font.Bold = true;
                customersSheet.Cells[$"B{customerRow}"].Value = data.Customers.Count();
                
                customerRow++;
                customersSheet.Cells[$"A{customerRow}"].Value = "Active Customers:";
                customersSheet.Cells[$"A{customerRow}"].Style.Font.Bold = true;
                customersSheet.Cells[$"B{customerRow}"].Value = data.ActiveCustomers;
                
                customerRow++;
                customersSheet.Cells[$"A{customerRow}"].Value = "Overdue Customers:";
                customersSheet.Cells[$"A{customerRow}"].Style.Font.Bold = true;
                customersSheet.Cells[$"B{customerRow}"].Value = data.OverdueCustomers;
                
                customersSheet.Cells.AutoFitColumns();
            }
            
            return package.GetAsByteArray();
        }
        
        public async Task<byte[]> GeneratePdfReport(ReportDataDto data, string reportType)
        {
            return await Task.Run(() =>
            {
                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(20);
                        page.DefaultTextStyle(x => x.FontSize(10));
                        
                        page.Header()
                            .AlignCenter()
                            .Text($"{reportType.ToUpper()} REPORT")
                            .FontSize(18)
                            .Bold()
                            .FontColor(QuestPDF.Helpers.Colors.Blue.Medium);
                        
                        page.Content()
                            .PaddingVertical(10)
                            .Column(column =>
                            {
                                // Generated date
                                column.Item()
                                    .AlignRight()
                                    .Text($"Generated: {DateTime.Now:dd/MM/yyyy HH:mm:ss}")
                                    .FontSize(9)
                                    .Italic();
                                
                                column.Item().PaddingTop(10);
                                
                                // Officer Details
                                column.Item().Row(row =>
                                {
                                    row.RelativeItem().Column(col =>
                                    {
                                        col.Item().Text("OFFICER DETAILS").FontSize(12).Bold().FontColor(QuestPDF.Helpers.Colors.Blue.Medium);
                                        col.Item().Text($"Name: {data.OfficerName}");
                                        col.Item().Text($"Email: {data.OfficerEmail}");
                                        col.Item().Text($"Role: {data.OfficerRole}");
                                    });
                                    
                                    row.RelativeItem().Column(col =>
                                    {
                                        col.Item().Text("PERFORMANCE SUMMARY").FontSize(12).Bold().FontColor(QuestPDF.Helpers.Colors.Blue.Medium);
                                        col.Item().Text($"Collection Rate: {data.CollectionRate}%");
                                        col.Item().Text($"Success Rate: {data.SuccessRate}%");
                                        col.Item().Text($"Total Collected: KES {data.TotalCollected:N0}");
                                    });
                                });
                                
                                column.Item().PaddingTop(15);
                                
                                // Key Metrics Table
                                column.Item().Text("KEY METRICS").FontSize(12).Bold().FontColor(QuestPDF.Helpers.Colors.Blue.Medium);
                                column.Item().Table(table =>
                                {
                                    table.ColumnsDefinition(columns =>
                                    {
                                        columns.RelativeColumn();
                                        columns.RelativeColumn();
                                        columns.RelativeColumn();
                                    });
                                    
                                    table.Header(header =>
                                    {
                                        header.Cell().Text("Metric").Bold();
                                        header.Cell().Text("Value").Bold();
                                        header.Cell().Text("Target").Bold();
                                    });
                                    
                                    table.Cell().Text("Total Customers");
                                    table.Cell().Text(data.TotalCustomers.ToString());
                                    table.Cell().Text("15");
                                    
                                    table.Cell().Text("Active Customers");
                                    table.Cell().Text(data.ActiveCustomers.ToString());
                                    table.Cell().Text("12");
                                    
                                    table.Cell().Text("Overdue Customers");
                                    table.Cell().Text(data.OverdueCustomers.ToString());
                                    table.Cell().Text("0");
                                    
                                    table.Cell().Text("Total Collections");
                                    table.Cell().Text($"KES {data.TotalCollected:N0}");
                                    table.Cell().Text("KES 400,000");
                                });
                                
                                // Transactions
                                if (data.Transactions != null && data.Transactions.Any())
                                {
                                    column.Item().PaddingTop(15);
                                    column.Item().Text("RECENT TRANSACTIONS").FontSize(12).Bold().FontColor(QuestPDF.Helpers.Colors.Blue.Medium);
                                    column.Item().Table(table =>
                                    {
                                        table.ColumnsDefinition(columns =>
                                        {
                                            columns.RelativeColumn(2);
                                            columns.RelativeColumn(3);
                                            columns.RelativeColumn(2);
                                            columns.RelativeColumn(2);
                                            columns.RelativeColumn(1);
                                        });
                                        
                                        table.Header(header =>
                                        {
                                            header.Cell().Text("Date").Bold();
                                            header.Cell().Text("Customer").Bold();
                                            header.Cell().Text("Amount").Bold();
                                            header.Cell().Text("Status").Bold();
                                            header.Cell().Text("Receipt").Bold();
                                        });
                                        
                                        foreach (var transaction in data.Transactions.Take(10))
                                        {
                                            table.Cell().Text(transaction.Date);
                                            table.Cell().Text(transaction.CustomerName);
                                            table.Cell().Text($"KES {transaction.Amount:N0}");
                                            table.Cell().Text(transaction.Status);
                                            table.Cell().Text(transaction.Receipt ?? "N/A");
                                        }
                                    });
                                }
                                
                                // Footer
                                column.Item().PaddingTop(20);
                                column.Item().AlignCenter().Text("Report generated by Collections System").FontSize(8).Italic();
                                column.Item().AlignCenter().Text("Powered by NCBA Group").FontSize(8).Italic();
                            });
                        
                        page.Footer()
                            .AlignCenter()
                            .Text(x =>
                            {
                                x.Span("Page ");
                                x.CurrentPageNumber();
                            });
                    });
                });
                
                return document.GeneratePdf();
            });
        }
    }
}