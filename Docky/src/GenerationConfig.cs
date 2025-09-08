namespace DockyApp;

public class GenerationConfig
{
    public string Model { get; set; } = "base";
    public bool AddRedis { get; set; }
    public bool AddRabbitMQ { get; set; }
    public bool AddOpenSearch { get; set; }
    public int RedisPort { get; set; } = 6379;
    public string? AdditionalServicePath { get; set; }
    public string OutputPath { get; set; } = "docker-compose.yml";
}
