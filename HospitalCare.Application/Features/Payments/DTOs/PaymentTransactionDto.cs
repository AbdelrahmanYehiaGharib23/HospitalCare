using System;
using System.Collections.Generic;
using System.Text;
using HospitalCare.Domain.Entities;
using HospitalCare.Domain.Enums;

namespace HospitalCare.Application.Features.Payments.DTOs
{
    public sealed record PaymentTransactionDto(int Id,int PatientId ,int DoctorId,int AppointmentTypeId,DateOnly RequestedDate,decimal Amount,PaymentStatus Status,string? GatewayReference,DateTime CreatedAt)
    {
        public static PaymentTransactionDto FromEntity(
            PaymentTransaction payment)
        {
            return new PaymentTransactionDto(
                payment.Id,
                payment.PatientId,
                payment.DoctorId,
                payment.AppointmentTypeId,
                payment.RequestedDate,
                payment.Amount,
                payment.Status,
                payment.GatewayReference,
                payment.CreatedAt);
        }
    }
}
