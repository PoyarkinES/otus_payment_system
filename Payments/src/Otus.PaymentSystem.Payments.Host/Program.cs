var builder = WebApplication.CreateBuilder(args);

// Добавляем поддержку стандартных ответов об ошибках (RFC 7807 ProblemDetails)
builder.Services.AddProblemDetails();

var app = builder.Build();

// Включаем обработчик исключений
app.UseExceptionHandler();
app.UseStatusCodePages();

// Базовый health-check (проверка, что процесс жив)
app.MapGet("/health/live", () => Results.Ok(new { status = "Healthy" }));

// Корневой эндпоинт для проверки
app.MapGet("/", () => Results.Ok(new
{
    service = "Payments.Host",
    status = "Running",
    timestamp = DateTime.UtcNow
}));

app.Run();
