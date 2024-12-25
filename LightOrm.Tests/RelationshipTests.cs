using System;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using LightOrm.Tests.Models.RelationshipExamples;

namespace LightOrm.Tests
{
    public class RelationshipTests
    {
        private readonly MySqlConnection _connection;

        public RelationshipTests(string connectionString)
        {
            _connection = new MySqlConnection(connectionString);
        }

        public async Task RunAllTests()
        {
            try
            {
                await _connection.OpenAsync();
                
                await InitializeDatabaseAsync();
                await TestOneToOneRelationshipAsync();
                await TestOneToManyRelationshipAsync();
                await TestManyToManyRelationshipAsync();
                await TestCustomAssociationTableAsync();
                await TestSelfReferencingRelationshipAsync();
            }
            finally
            {
                if (_connection.State != System.Data.ConnectionState.Closed)
                {
                    await _connection.CloseAsync();
                }
            }
        }

        private async Task InitializeDatabaseAsync()
        {
            Console.WriteLine("Initializing database tables...");

            // Drop foreign key constraints first
            var dropConstraintsCommands = new[]
            {
                "SET FOREIGN_KEY_CHECKS = 0",
                "DROP TABLE IF EXISTS employee_projects",
                "DROP TABLE IF EXISTS student_courses",
                "DROP TABLE IF EXISTS assignments",
                "DROP TABLE IF EXISTS students",
                "DROP TABLE IF EXISTS addresses",
                "DROP TABLE IF EXISTS courses",
                "DROP TABLE IF EXISTS employees",
                "DROP TABLE IF EXISTS departments",
                "DROP TABLE IF EXISTS projects",
                "SET FOREIGN_KEY_CHECKS = 1"
            };

            foreach (var command in dropConstraintsCommands)
            {
                using var cmd = new MySqlCommand(command, _connection);
                await cmd.ExecuteNonQueryAsync();
            }

            // Create tables without foreign keys first
            await new AddressModel().EnsureTableExistsAsync(_connection);
            await new CourseModel().EnsureTableExistsAsync(_connection);
            await new ProjectModel().EnsureTableExistsAsync(_connection);

            // Create tables with foreign keys but allow NULL for circular references
            using (var cmd = new MySqlCommand(@"
                CREATE TABLE IF NOT EXISTS employees (
                    Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                    first_name VARCHAR(50) NOT NULL,
                    last_name VARCHAR(50) NOT NULL,
                    email VARCHAR(255) NOT NULL,
                    hire_date DATETIME NOT NULL,
                    salary DECIMAL(18,2) NOT NULL,
                    department_id INT NULL,
                    supervisor_id INT NULL,
                    CreatedAt DATETIME NOT NULL,
                    UpdatedAt DATETIME NOT NULL
                )", _connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            using (var cmd = new MySqlCommand(@"
                CREATE TABLE IF NOT EXISTS departments (
                    Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
                    name VARCHAR(100) NOT NULL,
                    budget DECIMAL(18,2) NOT NULL,
                    location VARCHAR(100) NOT NULL,
                    head_employee_id INT NULL,
                    CreatedAt DATETIME NOT NULL,
                    UpdatedAt DATETIME NOT NULL
                )", _connection))
            {
                await cmd.ExecuteNonQueryAsync();
            }

            // Create remaining tables
            await new StudentModel().EnsureTableExistsAsync(_connection);
            await new AssignmentModel().EnsureTableExistsAsync(_connection);
            await new StudentCourseModel().EnsureTableExistsAsync(_connection);
            await new EmployeeProjectModel().EnsureTableExistsAsync(_connection);

            // Add foreign key constraints
            var alterTableCommands = new[]
            {
                "ALTER TABLE employees ADD CONSTRAINT fk_employee_department FOREIGN KEY (department_id) REFERENCES departments(Id)",
                "ALTER TABLE employees ADD CONSTRAINT fk_employee_supervisor FOREIGN KEY (supervisor_id) REFERENCES employees(Id)",
                "ALTER TABLE departments ADD CONSTRAINT fk_department_head FOREIGN KEY (head_employee_id) REFERENCES employees(Id)"
            };

            foreach (var command in alterTableCommands)
            {
                using var cmd = new MySqlCommand(command, _connection);
                await cmd.ExecuteNonQueryAsync();
            }

            Console.WriteLine("Tables created successfully!");
        }

        private async Task TestOneToOneRelationshipAsync()
        {
            Console.WriteLine("\nTesting One-to-One Relationship (Student-Address)...");

            // Create Address
            var address = new AddressModel
            {
                Street = "123 Main St",
                City = "Springfield",
                State = "IL",
                PostalCode = "62701"
            };
            await address.SaveAsync(_connection);
            Console.WriteLine($"Created address: {address.Street}, {address.City}");

            // Create Student with Address
            var student = new StudentModel
            {
                Name = "Jane Smith",
                Email = "jane@example.com",
                AddressId = address.Id
            };
            await student.SaveAsync(_connection);
            Console.WriteLine($"Created student: {student.Name}");

            // Load student with address
            var loadedStudent = await StudentModel.FindByIdAsync(_connection, student.Id, includeRelated: true);
            Console.WriteLine($"Loaded student: {loadedStudent.Name}");
            Console.WriteLine($"Student's address: {loadedStudent.Address.Street}, {loadedStudent.Address.City}");
        }

        private async Task TestOneToManyRelationshipAsync()
        {
            Console.WriteLine("\nTesting One-to-Many Relationship (Student-Assignments)...");

            // Create Student
            var student = new StudentModel
            {
                Name = "John Doe",
                Email = "john@example.com"
            };
            await student.SaveAsync(_connection);

            // Create Assignments for the student
            var assignment1 = new AssignmentModel
            {
                Title = "Math Homework",
                Description = "Complete exercises 1-10",
                DueDate = DateTime.Now.AddDays(7),
                Score = 0,
                StudentId = student.Id
            };
            await assignment1.SaveAsync(_connection);

            var assignment2 = new AssignmentModel
            {
                Title = "History Essay",
                Description = "Write about World War II",
                DueDate = DateTime.Now.AddDays(14),
                Score = 0,
                StudentId = student.Id
            };
            await assignment2.SaveAsync(_connection);

            // Load student with assignments
            var loadedStudent = await StudentModel.FindByIdAsync(_connection, student.Id, includeRelated: true);
            Console.WriteLine($"Student: {loadedStudent.Name}");
            Console.WriteLine("Assignments:");
            foreach (var assignment in loadedStudent.Assignments)
            {
                Console.WriteLine($"- {assignment.Title} (Due: {assignment.DueDate:d})");
            }
        }

        private async Task TestManyToManyRelationshipAsync()
        {
            Console.WriteLine("\nTesting Many-to-Many Relationship (Student-Courses)...");

            // Create Student
            var student = new StudentModel
            {
                Name = "Alice Johnson",
                Email = "alice@example.com"
            };
            await student.SaveAsync(_connection);

            // Create Courses
            var course1 = new CourseModel
            {
                Code = "CS101",
                Name = "Introduction to Programming",
                Credits = 3
            };
            await course1.SaveAsync(_connection);

            var course2 = new CourseModel
            {
                Code = "MATH201",
                Name = "Calculus I",
                Credits = 4
            };
            await course2.SaveAsync(_connection);

            // Create associations with additional data
            var enrollment1 = new StudentCourseModel
            {
                StudentId = student.Id,
                CourseId = course1.Id,
                EnrollmentDate = DateTime.Now,
                Grade = "A"
            };
            await enrollment1.SaveAsync(_connection);

            var enrollment2 = new StudentCourseModel
            {
                StudentId = student.Id,
                CourseId = course2.Id,
                EnrollmentDate = DateTime.Now,
                Grade = "B+"
            };
            await enrollment2.SaveAsync(_connection);

            // Load student with courses
            var loadedStudent = await StudentModel.FindByIdAsync(_connection, student.Id, includeRelated: true);
            Console.WriteLine($"Student: {loadedStudent.Name}");
            Console.WriteLine("Enrolled Courses:");
            foreach (var course in loadedStudent.Courses)
            {
                Console.WriteLine($"- {course.Code}: {course.Name} ({course.Credits} credits)");
            }
        }

        private async Task TestCustomAssociationTableAsync()
        {
            Console.WriteLine("\nTesting Custom Association Table (Employee-Project)...");

            // Create Employee
            var employee = new EmployeeModel
            {
                FirstName = "Bob",
                LastName = "Wilson",
                Email = "bob@example.com",
                HireDate = DateTime.Now.AddYears(-2),
                Salary = 75000
            };
            await employee.SaveAsync(_connection);

            // Create Project
            var project = new ProjectModel
            {
                Name = "Website Redesign",
                Description = "Modernize company website",
                StartDate = DateTime.Now,
                EndDate = DateTime.Now.AddMonths(3),
                Budget = 50000
            };
            await project.SaveAsync(_connection);

            // Create association with custom fields
            var assignment = new EmployeeProjectModel
            {
                EmployeeId = employee.Id,
                ProjectId = project.Id,
                Role = "Lead Developer",
                HoursAllocated = 120,
                StartDate = DateTime.Now,
                EndDate = DateTime.Now.AddMonths(3)
            };
            await assignment.SaveAsync(_connection);

            // Load employee with projects
            var loadedEmployee = await EmployeeModel.FindByIdAsync(_connection, employee.Id, includeRelated: true);
            Console.WriteLine($"Employee: {loadedEmployee.FirstName} {loadedEmployee.LastName}");
            Console.WriteLine("Assigned Projects:");
            foreach (var proj in loadedEmployee.Projects)
            {
                Console.WriteLine($"- {proj.Name} (Budget: ${proj.Budget:N0})");
            }
        }

        private async Task TestSelfReferencingRelationshipAsync()
        {
            Console.WriteLine("\nTesting Self-Referencing Relationship (Employee-Supervisor)...");

            // Create supervisor
            var supervisor = new EmployeeModel
            {
                FirstName = "Sarah",
                LastName = "Johnson",
                Email = "sarah@example.com",
                HireDate = DateTime.Now.AddYears(-5),
                Salary = 100000
            };
            await supervisor.SaveAsync(_connection);

            // Create employees reporting to supervisor
            var employee1 = new EmployeeModel
            {
                FirstName = "Mike",
                LastName = "Brown",
                Email = "mike@example.com",
                HireDate = DateTime.Now.AddYears(-1),
                Salary = 65000,
                SupervisorId = supervisor.Id
            };
            await employee1.SaveAsync(_connection);

            var employee2 = new EmployeeModel
            {
                FirstName = "Lisa",
                LastName = "Davis",
                Email = "lisa@example.com",
                HireDate = DateTime.Now.AddMonths(-6),
                Salary = 60000,
                SupervisorId = supervisor.Id
            };
            await employee2.SaveAsync(_connection);

            // Load supervisor with subordinates
            var loadedSupervisor = await EmployeeModel.FindByIdAsync(_connection, supervisor.Id, includeRelated: true);
            Console.WriteLine($"Supervisor: {loadedSupervisor.FirstName} {loadedSupervisor.LastName}");
            Console.WriteLine("Subordinates:");
            foreach (var subordinate in loadedSupervisor.Subordinates)
            {
                Console.WriteLine($"- {subordinate.FirstName} {subordinate.LastName}");
            }
        }
    }
}
