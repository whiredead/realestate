namespace ProjectAPI.Api.Application.AdminDashboards.Dashboard
{
    public class AgentPerformanceDto
    {
        public string AgentId { get; set; }
        public string AgentFullName { get; set; }
        public int SalesCount { get; set; }
        public decimal SalesVolume { get; set; }
        public int LeadsGenerated { get; set; }
        public int AppointmentsScheduled { get; set; }
        public int SuccessfulSales { get; set; }
        public double ConversionRate { get; set; }
    }

}
