using Microsoft.Data.SqlClient;
using RBI_Malaysia.Models;
using RBI_Malaysia.Services;
using Stimulsoft.Report;
using Stimulsoft.Report.Export;
using System.Data;
using System.Drawing.Printing;
//using static StudentRecorder.Components.Pages.StudentReport;

namespace StudentRecorder.Services
{
    public class RBIReportService
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public RBIReportService(
            IConfiguration configuration,
            IWebHostEnvironment environment)
        {
            _configuration = configuration;
            _environment = environment;
        }

        private SqlConnection GetConnection()
        {
            return new SqlConnection(
                _configuration.GetConnectionString("DefaultConnection"));
        }

        //public async Task<List<ProcessAreaModel>> GetNameAsync()
        //{
        //    List<ProcessAreaModel> studentName= new();

        //    using  SqlConnection con = GetConnection();
        //    string sql = @"
        //        SELECT
        //            Id,
        //            Name                   
        //        FROM Students
        //        ORDER BY Id DESC";
        //    SqlCommand cmd = new(sql, con);


        //    await con.OpenAsync();

        //    SqlDataReader reader = cmd.ExecuteReader();

        //    while (reader.Read())
        //    {
        //        studentName.Add(Map(reader));               
        //    }
        //    return studentName;
        //}
        //public static StudentReportItem Map(SqlDataReader reader)
        //{
        //    return new StudentReportItem
        //    {
        //        Id = Convert.ToInt32(reader["Id"]),
        //        Name = reader["Name"].ToString() ?? string.Empty,               
        //    };
        //}

        //public async Task<StiReport> GetStudentReportAsync()
        //{
        //    string connectionString =
        //        _configuration.GetConnectionString("DefaultConnection")!;


        //    string sql = @"
        //        SELECT
        //            Id,
        //            Name,
        //            Age,
        //            Department,
        //            Email
        //        FROM Students
        //        ORDER BY Id DESC";


        //    DataSet dataSet = new DataSet();


        //    await using (SqlConnection connection =
        //        new SqlConnection(connectionString))
        //    {
        //        await connection.OpenAsync();


        //        await using SqlCommand command =
        //            new SqlCommand(sql, connection);


        //        using SqlDataAdapter adapter =
        //            new SqlDataAdapter(command);


        //        adapter.Fill(dataSet, "Student");
        //    }


        //    StiReport report = new StiReport();


        //    string reportPath = Path.Combine(
        //        _environment.ContentRootPath,
        //        "Reports",
        //        "test.mrt"
        //    );


        //    report.Load(reportPath);
        //    //report.IsRendered = false;

        //    //report.Script = "";


        //    //report.Dictionary.Databases.Clear();

        //    //report.Dictionary.DataSources.Clear();


        //    report.RegData("Student", dataSet);


        //    report.Dictionary.Synchronize();


        //   report.Render();


        //    return report;
        //}



        //public async Task<byte[]> GetStudentReportPdfAsync()
        //{
        //    StiReport report =
        //        await GetStudentReportAsync();


        //    using MemoryStream stream =
        //        new MemoryStream();


        //    report.ExportDocument(
        //        StiExportFormat.Pdf,
        //        stream
        //    );


        //    return stream.ToArray();
        //}


        //public async Task<byte[]> GetStudentReportPdfAsync()
        //{
        //    var report = await GetStudentReportAsync();

        //    using MemoryStream ms = new MemoryStream();

        //    report.ExportDocument(
        //        Stimulsoft.Report.StiExportFormat.Pdf,
        //        ms
        //    );

        //    return ms.ToArray();
        //}

    }
}