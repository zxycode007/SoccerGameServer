using System;
using System.IO;
public class ServerLogger 
{
    public string logFileName  = "server.log";
    FileStream fs;
    StreamWriter writer;
    public ServerLogger()
    {
        
       // fs = new FileStream(logFileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
        writer = new StreamWriter(logFileName, true);
        
    }

    public void Log(string str)
    {
        Console.WriteLine("startup");
        try
        {
            DateTime dt = DateTime.Now;
            writer.WriteLine(dt.ToUniversalTime() + " " + str);
            writer.Flush();
        }catch(Exception e)
        {
            Console.WriteLine(e.Message);
        }
        
    }

    public void release()
    {
        if (writer != null)
        {
            writer.Close();
        } 
        if(fs != null)
        {
            fs.Close();
        }
        writer = null;
        fs = null;
    }

    
     
}