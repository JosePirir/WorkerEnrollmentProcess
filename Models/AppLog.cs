using System.Net;

namespace WorkerEnrollmentProcess.Models
{
    public class AppLog
    {
        public AppLog()
        {
            IPAddress[] address = Dns.GetHostEntry(HostName).AddressList;
            for(int i = 0; i<address.Length; i++)
            {
                Ip = address[i].ToString();
            }
        }
        public string Name { get; set; } = "WorkerEnrollmentManagement";
        public string HostName { get; set; } = Dns.GetHostName();
        public string ApiKey { get; set; } = "N/A";
        public string Uri { get; set; } = "N/A";
        public int ResponseCode { get; set; }
        public long ResponseTime { get; set; }
        public string Ip { get; set; }
        public int Level { get; set; }
        public string Message { get; set; }
        public string DateTime { get; set; }
        public string Version { get; set; } = "v1";
    }
}