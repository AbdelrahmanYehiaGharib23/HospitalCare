using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace HospitalCare.Domain.Entities
{

    public class Department : BaseEntity
    {
        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public ICollection<Doctor> Doctors { get; set; } = new List<Doctor>();
    }
}
