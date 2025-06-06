using LLama;
using LLama.Common;
using LLama.Sampling;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using MessageTesting;
using SimpleSockets;
using SimpleSockets.Messaging;
using SimpleSockets.Messaging.Metadata;
using SimpleSockets.Server;
using syscom;

/*
string modelPath = @"./mtx/Alita.gguf"; // change it to your own model path.

var parameters = new ModelParams(modelPath)
{
    ContextSize = 1024, // The longest length of chat as memory.
    GpuLayerCount = 5 // How many layers to offload to GPU. Please adjust it according to your GPU memory.
};
using var model = LLamaWeights.LoadFromFile(parameters);
using var context = model.CreateContext(parameters);
var executor = new InteractiveExecutor(context);

// Add chat histories as prompt to tell AI how to act.
var chatHistory = new ChatHistory();
chatHistory.AddMessage(AuthorRole.System, "Your name is Alita.You are a very kind and helpful assistant.Designed to simulate human behavior and emotions.");
chatHistory.AddMessage(AuthorRole.User, "Hello, Alita.");
chatHistory.AddMessage(AuthorRole.Assistant, "Hello. How may I help you today?");

ChatSession session = new(executor, chatHistory);
session.WithOutputTransform(new LLamaTransforms.KeywordTextOutputStreamTransform(
            new string[] { "User:", "Assistant:" },
            redundancyLength: 8));

InferenceParams inferenceParams = new InferenceParams()
{
    MaxTokens = 256, // No more than 256 tokens should appear in answer. Remove it if antiprompt is enough for control.
    AntiPrompts = new List<string> { "User:" }, // Stop generation once antiprompts appear.

    SamplingPipeline = new DefaultSamplingPipeline(),
};

Console.ForegroundColor = ConsoleColor.Yellow;
Console.Write("neural matrix initialized.\n: ");
Console.ForegroundColor = ConsoleColor.Green;
string userInput = Console.ReadLine() ?? "";
string out_data;

while (userInput != "exit")
{
    await foreach ( // Generate the response streamingly.
        var text
        in session.ChatAsync(
            new ChatHistory.Message(AuthorRole.User, userInput),
            inferenceParams))
    {
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write(text);
    }
    out_data = chatHistory.Messages.Last().Content;
    Console.Write("LAST DATA::: " + out_data);
    Console.ForegroundColor = ConsoleColor.Green;
    userInput = Console.ReadLine() ?? "";
}*/
// SERVER CODE
public class Alita
{
    public static string modelPath = @""; // change it to your own model path. Alita's gguf is top secret. pls download model from huggingface or other sources.
    public static LLamaWeights model;
    public static ModelParams parameters;
    public static InteractiveExecutor executor;
    public static ChatSession session;
    public static InferenceParams inferenceParams;

    public Alita()
    {
        parameters = new ModelParams(modelPath)
        {
            ContextSize = 1024, // The longest length of chat as memory.
            GpuLayerCount = 5 // How many layers to offload to GPU. Please adjust it according to your GPU memory.
        };
        model = LLamaWeights.LoadFromFile(parameters);
        executor = new InteractiveExecutor(model.CreateContext(parameters));

        // Add chat histories as prompt to tell AI how to act.
        var chatHistory = new ChatHistory();
        chatHistory.AddMessage(AuthorRole.System, ""); //modify prompt and change history to your own.
        chatHistory.AddMessage(AuthorRole.User, "");
        chatHistory.AddMessage(AuthorRole.Assistant, "");

        session = new(executor, chatHistory);
        session.WithOutputTransform(new LLamaTransforms.KeywordTextOutputStreamTransform(
                    new string[] { "User:", "Assistant:"},
                    redundancyLength: 8));

        inferenceParams = new InferenceParams()
        {
            MaxTokens = 256, // No more than 256 tokens should appear in answer. Remove it if antiprompt is enough for control.
            AntiPrompts = new List<string> { "User:" }, // Stop generation once antiprompts appear.

            SamplingPipeline = new DefaultSamplingPipeline(),
        };
        //Console.Clear();
    }
    public static async Task<string> GetResponse(string userInput)
    {
        string out_data = "";
        await foreach ( // Generate the response streamingly.
            var text
            in session.ChatAsync(
                new ChatHistory.Message(AuthorRole.User, userInput),
                inferenceParams))
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(text);
            out_data += text;
        }
        Server.SendMessage(out_data);
        return out_data;
    }
    public static void Dispose()
    {
        model.Dispose();
    }
    public static void Main(string[] args)
    {
        //AUXILIARY
        //Server S = new Server();
        Alita alita = new Alita();
        Server.TESTING();
        while (true)
        {
            Console.Write(">> ");
            string userInput = Console.ReadLine() ?? "";
            if (userInput.ToLower() == "exit")
                break;

            string response = Alita.GetResponse(userInput).Result;
            //Console.WriteLine("Response: " + response);
        }
        /*string userInput = "Hello, Alita.";
        string response = Alita.GetResponse(userInput).Result;
        Console.WriteLine("Response: " + response);
        Alita.Dispose();*/
    }
}
// This is a simple console application that initializes the Alita model and generates a response to a user input.