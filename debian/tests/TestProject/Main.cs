using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Google.Apis;

namespace TestProject
{
    class Program
    {
	static void Main(string[] args)
	{
		Stack<int> myStack = new Stack<int>();
		var th = new Thread(()=>WaitAndPrint(myStack));
		th.Start();
	    	Console.WriteLine("Me first!");
	    	myStack.Push(1);
	    	Console.WriteLine("Finished tasks: {0}", myStack.Count);
	    	Thread.Sleep(1000);
	    	Console.WriteLine("Finished tasks: {0}", myStack.Count);
	}
	    
	private static void WaitAndPrint(Stack<int> myStack){
	    	Thread.Sleep(1000);
	    	Console.WriteLine("Me second!");
	    	myStack.Push(2);
	}
    }
}
