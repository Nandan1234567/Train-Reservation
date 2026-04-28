namespace TrainBooking.Hosting;

public static class Worker
{
    public static IHostBuilder CreateBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                services.AddHostedService<WorkerService>();
            });)
}
