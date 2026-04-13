// Models/DTOs/PromiseDtos.cs
using System;

namespace RekovaBE_CSharp.Models.DTOs
{
    public class PromiseDto
    {
        public int Id { get; set; }
        public string PromiseId { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? PhoneNumber { get; set; }
        public decimal PromiseAmount { get; set; }
        public DateTime PromiseDate { get; set; }
        public string? PromiseType { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal? FulfillmentAmount { get; set; }
        public DateTime? FulfillmentDate { get; set; }
        public string? Notes { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CreatePromiseDto
    {
        public int CustomerId { get; set; }
        public decimal PromiseAmount { get; set; }
        public DateTime PromiseDate { get; set; }
        public string? PromiseType { get; set; }
        public string? Notes { get; set; }
    }

    public class UpdatePromiseStatusDto
    {
        public string Status { get; set; } = string.Empty;
        public decimal? FulfillmentAmount { get; set; }
    }

    public class UpdatePromiseDto
    {
        public decimal? PromiseAmount { get; set; }
        public DateTime? PromiseDate { get; set; }
        public string? PromiseType { get; set; }
        public string? Notes { get; set; }
    }
}