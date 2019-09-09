using System;

namespace MiserableOrAdorable.Dto
{
    public class Employee
    {
        public Employee(Guid id, string fullName, int age)
        {
            Id = id;
            FullName = fullName;
            Age = age;
        }

        public Guid Id { get; set; }

        public string FullName { get; set; }

        public int Age { get; set; }

        public bool IsApproved { get; set; }
    }
}