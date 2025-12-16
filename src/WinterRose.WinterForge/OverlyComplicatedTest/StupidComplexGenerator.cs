namespace WinterRose.WinterForgeSerializing.OverlyComplicatedTest
{
    public static class StupidComplexGenerator
    {
        public static List<Company> Generate(
            int numCompanies = 200,
            int departmentsPerCompany = 30,
            int peoplePerDepartment = 50,
            int projectsPerCompany = 15,
            int maxTeamSize = 14,
            int seed = 1337)
        {
            var rng = new Random(seed);
            var companies = new List<Company>();

            for (int c = 0; c < numCompanies; c++)
            {
                var company = new Company { Name = $"Company_{c}" };

                // create people pool
                var peoplePool = new List<Person>();
                for (int d = 0; d < departmentsPerCompany; d++)
                {
                    for (int p = 0; p < peoplePerDepartment; p++)
                    {
                        var person = new Person
                        {
                            Name = $"Person_{c}_{d}_{p}",
                            Age = rng.Next(20, 60)
                        };
                        peoplePool.Add(person);
                    }
                }

                // create departments
                for (int d = 0; d < departmentsPerCompany; d++)
                {
                    var department = new Department { Name = $"Dept_{c}_{d}" };
                    // assign people to department
                    for (int i = 0; i < peoplePerDepartment; i++)
                    {
                        var personIndex = d * peoplePerDepartment + i;
                        if (personIndex < peoplePool.Count)
                            department.Members.Add(peoplePool[personIndex]);
                    }
                    company.Departments.Add(department);
                }

                // create projects
                for (int pr = 0; pr < projectsPerCompany; pr++)
                {
                    var project = new Project
                    {
                        Name = $"Project_{c}_{pr}",
                        Budget = rng.Next(50000, 200000)
                    };

                    // assign a random team
                    int teamSize = rng.Next(1, Math.Min(maxTeamSize, peoplePool.Count) + 1);
                    var teamIndices = new HashSet<int>();
                    while (teamIndices.Count < teamSize)
                        teamIndices.Add(rng.Next(peoplePool.Count));

                    foreach (var idx in teamIndices)
                    {
                        var person = peoplePool[idx];
                        project.Team.Add(person);
                        person.Projects.Add(project); // back-reference, no loops
                    }

                    company.Projects.Add(project);
                }

                companies.Add(company);
            }

            return companies;
        }

        public class Company
        {
            public string Name;
            public List<Department> Departments = new();
            public List<Project> Projects = new();
        }

        public class Department
        {
            public string Name;
            public List<Person> Members = new();
        }

        public class Project
        {
            public string Name;
            public double Budget;
            public List<Person> Team = new();
        }

        public class Person
        {
            public string Name;
            public int Age;
            public List<Project> Projects = new();
        }
    }
}
