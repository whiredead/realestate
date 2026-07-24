public class PerformanceIndicator
{
    public Guid Id { get; set; }
    public string? AgentId { get; set; }
    public int LeadsGenerated { get; set; } // Number of leads generated
    public int AppointmentsScheduled { get; set; } // Number of appointments scheduled
    public int SuccessfulSales { get; set; } // Number of sales completed

    public double ConversionRate => LeadsGenerated == 0 ? 0 : (SuccessfulSales / (double)LeadsGenerated) * 100;

    public DateTime RecordedAt { get; set; }

    public void IncrementLeadsGenerated()
    {
        LeadsGenerated++;
    }

    public void IncrementAppointmentsScheduled()
    {
        AppointmentsScheduled++;
    }

    public void IncrementSuccessfulSales()
    {
        SuccessfulSales++;
    }
}
