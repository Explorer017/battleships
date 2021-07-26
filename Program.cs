using System;
using System.Threading;
using Spectre.Console;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Text.Unicode;
using System.Text;

namespace battleships
{
    class Program
    {
        public static Socket handler;
        public static void Main(string[] args)
        {
            var mode = AnsiConsole.Prompt(
                new TextPrompt<string>("Would you like to [darkorange]host [/]or [aqua]join[/]?")
                    .InvalidChoiceMessage("[red]That is not a valid option![/]")
                    .DefaultValue("join")
                    .AddChoice("join")
                    .AddChoice("host")
                    .AddChoice("test"));
            
            if (mode == "join"){
                Connect("localhost");
            } else if (mode == "host"){
                Host();
            }
            else {
                placeBoats();
                // throw new Exception("something went very wrong");
            }
            // Synchronous
            // AnsiConsole.Status()
            //     .Start("Atempting to Connect to Server...", ctx => 
            //     {
            //         // Simulate some work
            //         ctx.Spinner(Spinner.Known.Dots6);
            //         ctx.SpinnerStyle(Style.Parse("aqua"));
            //         Thread.Sleep(1000);
                    
            //         // Update the status and spinner
            //         ctx.Status("");
            //         ctx.Spinner(Spinner.Known.Balloon2);
            //         ctx.SpinnerStyle(Style.Parse("aqua"));

            //         // Simulate some work
            //         AnsiConsole.MarkupLine("Doing some more work...");
            //         Thread.Sleep(2000);
            //     });


        }

        public static void Host(){
            AnsiConsole.Status()
                .Start("Setting up Server...", ctx =>
                {
                    ctx.Spinner(Spinner.Known.Bounce);
                    ctx.SpinnerStyle(Style.Parse("darkorange"));
                    IPHostEntry host = Dns.GetHostEntry("localhost");  
                    IPAddress ipAddress = host.AddressList[0];  
                    IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 11000);
                    ctx.Status("Binding Port...");
                    try {
                        // Create a Socket that will use Tcp protocol      
                        Socket listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);  
                        // A Socket must be associated with an endpoint using the Bind method  
                        listener.Bind(localEndPoint);  
                        // Specify how many requests a Socket can listen before it gives Server busy response.  
                        // We will listen 10 requests at a time  
                        listener.Listen(100);
                        ctx.Status("Waiting for a connection...");
                        try{
                            handler = listener.Accept();
                        } catch(Exception e){
                            AnsiConsole.WriteException(e);
                        }
                        
                        ctx.SpinnerStyle(Style.Parse("green"));
                    } catch (Exception e){
                        AnsiConsole.WriteException(e);
                    }
                });
                byte[] msg = Encoding.ASCII.GetBytes("wait");  
                handler.Send(msg);  
                string[,] myGrid = placeBoats();
                msg = Encoding.ASCII.GetBytes("done");  
                handler.Send(msg); 

                AnsiConsole.Status().Start("Waiting for the oppontant to place their boats...", ctx =>
                    {
                        byte[] bytes = new byte[1024];
                        int bytesRec = handler.Receive(bytes);
                    }
                );
                string [,] enemyGrid = SetupGrid();
                enemyGrid = (myTurn(enemyGrid, handler));
                msg = Encoding.ASCII.GetBytes("done");  
                handler.Send(msg);
                    AnsiConsole.Status().Start("It is the other players turn", ctx =>
                    {
                        byte[] bytes = new byte[1024];
                        int bytesRec = handler.Receive(bytes);
                        
                        byte[] toSend = new byte[1];
                        toSend[0] = Convert.ToByte(hitDetector(Encoding.ASCII.GetString(bytes,0,bytesRec),myGrid));
                        handler.Send(toSend);

                        bytes = new byte[1024];
                        bytesRec = handler.Receive(bytes);
                    }
                );
        }

        public static void Connect(string ip){
            IPHostEntry host = Dns.GetHostEntry(ip);  
            IPAddress ipAddress = host.AddressList[0];  
            IPEndPoint remoteEP = new IPEndPoint(ipAddress, 11000);  
            // Create a TCP/IP  socket.    
            Socket sender = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp); 
            AnsiConsole.Status()
                .Start("Connecting to Server...", ctx =>
                {
                    sender.Connect(remoteEP);
                    Console.WriteLine("Socket connected to {0}", sender.RemoteEndPoint.ToString());  
                    byte[] bytes = new byte[1024];
                    int bytesRec = sender.Receive(bytes);  
                    Console.WriteLine("Echoed test = {0}",  
                        Encoding.ASCII.GetString(bytes, 0, bytesRec));
                    ctx.Status("Waiting for your oponant to place his ships");
                    bytes = new byte[1024];
                    bytesRec = sender.Receive(bytes);  
                    Console.WriteLine("Echoed test = {0}",  
                        Encoding.ASCII.GetString(bytes, 0, bytesRec));
                }
                );
            string[,] myGrid = placeBoats();
            byte[] msg = Encoding.ASCII.GetBytes("done");  
            sender.Send(msg);
            string [,] enemyGrid = SetupGrid();
            AnsiConsole.Status().Start("It is the other players turn", ctx =>
                    {
                        byte[] bytes = new byte[1024];
                        int bytesRec = sender.Receive(bytes);
                        
                        byte[] toSend = new byte[1];
                        toSend[0] = Convert.ToByte(hitDetector(Encoding.ASCII.GetString(bytes,0,bytesRec),myGrid));
                        sender.Send(toSend);

                        bytes = new byte[1024];
                        bytesRec = sender.Receive(bytes);
                    }
                );
            AnsiConsole.MarkupLine("Your Turn");
            Console.ReadLine();
            enemyGrid = (myTurn(enemyGrid, sender));
            msg = Encoding.ASCII.GetBytes("done");  
            sender.Send(msg);

        }

        public static string[,] SetupGrid(){
            // create the variable
            string [,] grid = new string[14,15];
            // filling the variable
            char c2 = 'A';
            for (int i = 0 ; i != 14; i++){
                grid[i,0] = c2.ToString();
                c2++;
                for (int j = 1; j != 15; j++){
                    grid [i,j] = "[]".EscapeMarkup();
                }
            }
            // foreach (string i in grid)
            // {
            //     var rendered = Emoji.Replace(i);
            //     AnsiConsole.Markup(rendered);
            // }
            //grid[0,1] = '⬛';
            // update the table
            

            return grid;
        }

        public static void updateTable(string[,] grid){

            Table table = new Table();
            // Add some columns
            table.AddColumn("");
            for(int i = 0; i != 14; i++){
                table.AddColumn(new TableColumn($"{i+1}").Centered());
            }

            for(int i = 0; i != 14; i++){
                table.AddRow($"{grid[i,0]}",$"{grid[i,1]}",$"{grid[i,2]}",$"{grid[i,3]}",$"{grid[i,4]}",$"{grid[i,5]}",$"{grid[i,6]}",$"{grid[i,7]}",$"{grid[i,8]}",$"{grid[i,9]}",$"{grid[i,10]}",$"{grid[i,11]}",$"{grid[i,12]}",$"{grid[i,13]}",$"{grid[i,14]}");
            }

            // Render the table to the console
            AnsiConsole.Render(table);

        }

        public static string[,] placeBoats(){
            string[,] grid = SetupGrid();
            updateTable(grid);

            string[] shipNames = {"Destroyer", "Submarine", "Cruiser", "Battleship", "Carrier"};
            int[] shipSizes = {2,2,3,4,5};
            int i = 0;
            foreach (string j in shipNames){
                string place = AnsiConsole.Ask<string>($"Where do you want to put your [green]{j}[/] ({shipSizes[i]} long)?");
                int number;
                try{
                    number = Int32.Parse(place.Remove(0,1));
                    if (Regex.IsMatch(place[0].ToString(), "[A-N]") && number <= 14 && number > 0){}
                    else{throw new Exception("reeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee");}
                } catch (Exception e) {throw e;}
                var mode = AnsiConsole.Prompt(
                new TextPrompt<string>("Would you like it to be [purple]Horizontal [/]or [teal]Vertical[/]?")
                    .InvalidChoiceMessage("[red]That is not a valid option![/]")
                    .DefaultValue("Horizontal")
                    .AddChoice("Horizontal")
                    .AddChoice("Vertical"));
                // TODO: make exeptions for overlap
                // TODO: handle exeptions
                if (mode == "Horizontal"){
                    char[] test = {'A','B','C','D','E','F','G','H','I','J','K','L','M','N'};
                    int row = -1;
                    int k = 0;
                    foreach(char rowLetters in test){
                        if (rowLetters == place[0]){
                            row = k;
                        }
                        else{
                            k++;
                        }
                    }
                    AnsiConsole.MarkupLine($"{place[0]} is row number {row}");
                    for (int l = 0; l!=shipSizes[i]; l++){
                        grid[row,number+l] = $"[green]{"[]".EscapeMarkup()}[/]";
                    }
                } else if (mode == "Vertical"){
                    char[] test = {'A','B','C','D','E','F','G','H','I','J','K','L','M','N'};
                    int row = -1;
                    int k = 0;
                    foreach(char rowLetters in test){
                        if (rowLetters == place[0]){
                            row = k;
                        }
                        else{
                            k++;
                        }
                    }
                    AnsiConsole.MarkupLine($"{place[0]} is row number {row}");
                    for (int l = 0; l!=shipSizes[i]; l++){
                        grid[row+l,number] = $"[green]{"[]".EscapeMarkup()}[/]";
                    }
                }
                i++;
                updateTable(grid);
            }


            return grid;
        }

        public static string[,] myTurn(string [,] enemyGrid, Socket link){
            AnsiConsole.MarkupLine("Showing: [red]ENEMIES GRID[/]");
            updateTable(enemyGrid);
            string place = AnsiConsole.Ask<string>($"Where do you want to [red]fire[/] at?");
            int number = -1;
            try{
                number = Int32.Parse(place.Remove(0,1));
                if (Regex.IsMatch(place[0].ToString(), "[A-N]") && number <= 14 && number > 0){}
                else{throw new Exception("reeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee");}
            } catch (Exception e) {AnsiConsole.MarkupLine($"Something went wrong! ({e}) \n[red]The program may no longer function[/]\nIT IS HIGHLY RECOMENDED YOU RESTART YOUR GAME!");}
            link.Send(Encoding.ASCII.GetBytes(place));
            AnsiConsole.Status().Start("please wait", ctx =>
                {
                    byte[] bytes = new byte[1024];
                    int bytesRec = link.Receive(bytes);
                    bool hit = Convert.ToBoolean(bytes[0]);
                    if (hit == true){
                        AnsiConsole.MarkupLine("[red]HIT![/]");
                        try{
                            enemyGrid[row(place[0]),number] = $"[red]X[/]";
                            updateTable(enemyGrid);
                        } catch{}

                    }else {
                        AnsiConsole.MarkupLine("[yellow]MISS![/]");
                        try{
                            enemyGrid[row(place[0]),number] = $"[yellow]O[/]";
                            updateTable(enemyGrid);
                        } catch{}    
                    }
                    
                }
            );
            return enemyGrid;
        }
        public static bool hitDetector(string coordinate, string[,] grid){
            int therow = row(coordinate[0]);
            try{int number = Int32.Parse(coordinate.Remove(0,1));
            if (grid[therow,number] == $"[green]{"[]".EscapeMarkup()}[/]"){
                    grid[therow,number] = $"[green]{"[".EscapeMarkup()}[/][red]X[/][green]{"]".EscapeMarkup()}[/]";
                    updateTable(grid);
                    AnsiConsole.MarkupLine($"The opponent [red]HIT[/] [green]{coordinate}[/]");
                    return true;
            }else{
                grid[therow,number] = $"[yellow]O[/]";
                updateTable(grid);
                AnsiConsole.MarkupLine($"The opponent fired at [green]{coordinate}[/] and [yellow]MISSED[/] ");
                return false;
            }}
            catch(Exception e){AnsiConsole.MarkupLine($"Something went wrong! ({e}) \nThe program may no longer function\nIT IS HIGHLY RECOMENDED YOU RESTART YOUR GAME!".EscapeMarkup());
                               return false;}
            
        }
        public static int row(char bob){
            char[] test = {'A','B','C','D','E','F','G','H','I','J','K','L','M','N'};
            int row = -1;
            int k = 0;
            foreach(char rowLetters in test){
                if (rowLetters == bob){
                    row = k;
                }
                else{
                    k++;
                }
            }
            return row;
        }
    }
}
