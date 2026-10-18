using System;
using System.Collections.Generic;
using System.Text;
using HospitalCare.Domain.Entities;

namespace HospitalCare.Application.Features.Departments.DTOs
{
    public sealed record DepartmentDto( int Id,string Name,bool IsActive)
    {
        public static DepartmentDto FromEntity(Department department)
        {
            return new DepartmentDto(
                department.Id,
                department.Name,
                department.IsActive);
        }
    }
}