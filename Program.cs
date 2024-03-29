﻿using System.Text;
using System.Text.Json;
using Serilog;
using WorkerEnrollmentProcess.Models;
using ServiceReference;/*namespace de reference.cs*/
using RabbitMQ.Client.Events;
using RabbitMQ.Client;

public class Program
{
    private static AppLog AppLog = new AppLog();
    private static IModel channel  = null;/*conexion hacia rabbit*/
    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration().WriteTo.File("WorkerEnrollmentManagment.out",Serilog.Events.LogEventLevel.Debug, "{Message:lj}{NewLine}", encoding: Encoding.UTF8).CreateLogger();
        /*configuracion para los logs, para mandar a imprimir*/
        AppLog.ResponseTime = Convert.ToInt16(DateTime.Now.ToString("fff"));
        AppLog.DateTime = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss");
        AppLog.ResponseCode = 0;
        IConnection conexion = null;/*crear coneccion hacia rabbit*/
        var connectionFactory = new ConnectionFactory();
        connectionFactory.Port = 5672;
        connectionFactory.UserName = "guest";
        connectionFactory.Password = "guest";
        conexion = connectionFactory.CreateConnection();
        channel = conexion.CreateModel();
        var consumerEnrollment = new EventingBasicConsumer(channel);/*se consumira por este canal*/
        var consumerCandidateRecord = new EventingBasicConsumer(channel);
        consumerEnrollment.Received += ConsumerReceived;/*leer el mensaje de la cola*/
        consumerCandidateRecord.Received += ConsumerReceivedCandidateRecordReceived;
        var consumerTag = channel.BasicConsume("kalum.queue.enrollment",true, consumerEnrollment); /*se creo la cola de forma exitosa*/
        var consumerTagCandidateRecordCreate = channel.BasicConsume("kalum.queue.candidate-record",true,consumerCandidateRecord);
        Console.WriteLine("Presione enter para finalizar el proceso");
        Console.ReadLine();
    }

    public static async void ConsumerReceived(object? sender, BasicDeliverEventArgs e) /*recibe un objeto opcional y un basic*/
    {
        string message = Encoding.UTF8.GetString(e.Body.ToArray());/*leer el body*/
        EnrollmentRequest request = JsonSerializer.Deserialize<EnrollmentRequest>(message); /*convierte el objeto de tipo string a un objeto tipo EnorlllmentRequest*/
        ImprimirLog(0,JsonSerializer.Serialize(request),"Debug");
        EnrollmentResponse enrollmentResponse = await ClientWebService(request);
        if(enrollmentResponse != null && enrollmentResponse.Codigo != 201)
        {
            channel.BasicPublish("", "kalum.queue.failed-enrollment", null, Encoding.UTF8.GetBytes(message));
        }
        else
        {
            AppLog.Message = "Se proceso el proceso de inscripcion de forma exitosa";
            ImprimirLog(201, JsonSerializer.Serialize(AppLog),"Information");
        }
    }

    public static async void ConsumerReceivedCandidateRecordReceived(object? sender, BasicDeliverEventArgs e)
    {
        string message = Encoding.UTF8.GetString(e.Body.ToArray());
        CandidateRecordRequest request = JsonSerializer.Deserialize<CandidateRecordRequest>(message);
        ImprimirLog(0,JsonSerializer.Serialize(request),"Debug");
        CandidateRecordResponse candidateRecordResponse = await ClientWebServiceCandidateRecord(request);
        if(candidateRecordResponse != null && candidateRecordResponse.Codigo != 201)
        {
            channel.BasicPublish("","kalum.queue.failed-enrollment",null,Encoding.UTF8.GetBytes(message));
        } 
        else 
        {
            AppLog.Message = $"Se creo exitosamente el registro del expediente con número {candidateRecordResponse.NoExpediente}";
            ImprimirLog(201,JsonSerializer.Serialize(AppLog),"Information");
        }
    }

    public static async Task<EnrollmentResponse> ClientWebService(EnrollmentRequest request)/*Va a devolver un objeto de tipo enrollmentresponse*//*EnrollmentResponse trae la estructura de la respuesta de web services*/
    {
        EnrollmentResponse enrollmentResponse = null;
        try
        {
        var client = new EnrollmentServiceClient(EnrollmentServiceClient.EndpointConfiguration.BasicHttpBinding_IEnrollmentService_soap, "http://localhost:5253/EnrollmentService.asmx" /*el servicio que se va a consumir*/);/*el cliente consume el servicio*/
        var response = await client.EnrollmentProcessAsync(request);
        enrollmentResponse = new EnrollmentResponse()
        {
            Codigo = response.Body.EnrollmentProcessResult.Codigo, 
            Respuesta = response.Body.EnrollmentProcessResult.Respuesta, 
            Carne = response.Body.EnrollmentProcessResult.Carne
        };/*respuesta cuando se consuma el servicio para que tenga estructurado lo que se va a recibir*/ /*<codigo>*/
        if(enrollmentResponse.Codigo == 503)
        {
            ImprimirLog(503, enrollmentResponse.Respuesta, "Error");
        }
        else
        {
            ImprimirLog(201, $"Se finalizó el proceso de inscripcion con el carné {enrollmentResponse.Carne}", "Information");
        }
        }
        catch(Exception e)
        {
            enrollmentResponse = new EnrollmentResponse() {Codigo = 500, Respuesta = e.Message, Carne = "0"};
            ImprimirLog(500, $"Error en el sistema: {e.Message}", "Error");
        }
        return enrollmentResponse;
    }

    public static async Task<CandidateRecordResponse> ClientWebServiceCandidateRecord(CandidateRecordRequest request)
    {
        CandidateRecordResponse candidateRecordResponse = null;
        try
        {
            var client = new EnrollmentServiceClient(EnrollmentServiceClient.EndpointConfiguration.BasicHttpBinding_IEnrollmentService_soap, "http://localhost:5253/EnrollmentService.asmx");
            var response = await client.CandidateRegisterProcessAsync(request);
            candidateRecordResponse = new CandidateRecordResponse()
            {
                Codigo = response.Body.CandidateRegisterProcessResult.Codigo,
                Respuesta = response.Body.CandidateRegisterProcessResult.Respuesta,
                NoExpediente = response.Body.CandidateRegisterProcessResult.NoExpediente
            };
            if(candidateRecordResponse.Codigo == 503)
            {
                ImprimirLog(503, candidateRecordResponse.Respuesta, "Error");    
            }
            else 
            {
                ImprimirLog(201,$"Se finalizo el proceso de inscripción con el Expediente {candidateRecordResponse.NoExpediente}","Information");
            }
        }
        catch(Exception e)
        {
            candidateRecordResponse = new CandidateRecordResponse() {Codigo = 500, Respuesta = e.Message, NoExpediente = "0"};
            ImprimirLog(500, $"Error en el sistema: {e.Message}","Error");

        }
        return candidateRecordResponse;
    }

    /*metodo para consumir el servicio de rabbit*/

    private static void ImprimirLog(int responseCode, string message, string typeLog)
    {
        AppLog.ResponseCode = responseCode;
        AppLog.Message = message;
        AppLog.ResponseTime = Convert.ToInt16(DateTime.Now.ToString("fff")) - AppLog.ResponseTime;
        if(typeLog.Equals("Error"))
        {
            AppLog.Level = 40;
            Log.Error(JsonSerializer.Serialize(AppLog));
        }
        else if(typeLog.Equals("Information"))
        {
            AppLog.Level = 20;
            Log.Information(JsonSerializer.Serialize(AppLog));
        }
        else if (typeLog.Equals("Debug"))
        {
            AppLog.Level = 10;
            Log.Debug(JsonSerializer.Serialize(AppLog));
        }
    }
}