namespace EthicsQA.API;

#nullable disable
public class Configuration
{
    public string OpenAI_API_KEY { get; set; }
    public string DB_Connection_String { get; set; }
    public string DB_Name { get; set; }
    public readonly string DB_User_Container = "Users";
    public string Communication_Services_Connection_String { get; set; }
    public string Communication_Services_Phone { get; set; }
    public string JWT_Secret { get; set; }
}

#nullable restore