
#region init path/file
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

Console.Title = "live_tts_chatgpt";
FileHelper.CreatePath("models");
FileHelper.CreatePath("output");
FileHelper.CreateFile("output/currText.txt");
#endregion

#region api conifg
var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";
var builder = WebApplication.CreateBuilder(args);


// �������
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
                      policy =>
                      {
                          policy.AllowAnyOrigin();
                          //policy.WithOrigins("http://0.0.0.0:*");
                          //policy.WithOrigins("http://0.0.0.0:4000");

                      });
});

var app = builder.Build();
app.UseExceptionHandler("/Error");
app.UseCors(MyAllowSpecificOrigins);
//������ 3939 �˿�
app.Urls.Add("http://0.0.0.0:3939");
#endregion

// obs keyboard input effect
app.MapGet("/keyboard", () => new SubtitleHelper().RealText);
app.MapGet("/top", () => "ok");


app.Run();
Console.WriteLine("����");



var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
