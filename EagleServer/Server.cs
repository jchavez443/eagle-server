
using System.Threading;
using System.Collections.Generic;
using System.Text;
using System;
using System.Threading.Tasks;
using System.Net;
using Newtonsoft.Json;

using EagleServer.Exceptions;
using Newtonsoft.Json.Linq;

namespace Eagle {

	/**
	This is a light weight server, @author JLC
	 */
	public class Server {

		private static Server server = null;

        private static HttpListener listner = null;

        private static readonly object sync = new object();

		private static string _port;

        private static bool _isHttp = false;

		private static bool _stop = false;

        private static Task MainLoop = null;

		private static Dictionary<string, object> postMappings = new Dictionary<string, object>();

		private static Dictionary<string, object> getMappings = new Dictionary<string, object>();

        private static Dictionary<string, object> putMappings = new Dictionary<string, object>();

        private static Dictionary<string, object> deleteMappings = new Dictionary<string, object>();


        private static IAsyncResult res;

        private Server(){

			 if (!HttpListener.IsSupported)
            {
                throw new HttpListenerNotSupported();
            }

			listner = new HttpListener();

            if (!_isHttp)
            {
                if (_port == null || _port.Length == 0)
                {
                    listner.Prefixes.Add("https://+:8080/");
                }
                else
                {
                    listner.Prefixes.Add("https://+:" + _port + "/");
                }
            }
            else
            {
                if (_port == null || _port.Length == 0)
                {
                    listner.Prefixes.Add("http://+:8080/");
                }
                else
                {
                    listner.Prefixes.Add("http://+:" + _port + "/");
                }
            }

            try
            {
                listner.Start();
            } catch (Exception e)
            {
                throw e;
            }

            object innerSync = new object();

			MainLoop = Task.Run( () => {

                HashSet<Task> runningTasks = new HashSet<Task>();
				
				while (!_stop)
				{

					res = listner.BeginGetContext(ListenerCallback, listner);
                    res.AsyncWaitHandle.WaitOne(5000);
					
                }

                //Task.WhenAll(runningTasks).Wait();

                if(listner.IsListening)
				    listner.Stop();

				listner.Close();
			});

		}

        public static void ListenerCallback(IAsyncResult result)
        {
            HttpListener listener = (HttpListener)result.AsyncState;
            // Use EndGetContext to complete the asynchronous operation.
            
            HttpListenerContext ctx;
            try
            {
                ctx = listener.EndGetContext(result);
            } catch (ObjectDisposedException ex)
            {
                return;
            }

            object func = null;
            try
            {
                string path = ctx.Request.RawUrl;
                string body = null;
                if ("POST".Equals(ctx.Request.HttpMethod) && postMappings.ContainsKey(path))
                {
                    func = postMappings[path];
                }
                else if ("GET".Equals(ctx.Request.HttpMethod) && getMappings.ContainsKey(path))
                {
                    func = getMappings[path];
                }
                else if ("DELETE".Equals(ctx.Request.HttpMethod) && getMappings.ContainsKey(path))
                {
                    func = deleteMappings[path];

                }
                else if ("PUT".Equals(ctx.Request.HttpMethod) && getMappings.ContainsKey(path))
                {
                    func = putMappings[path];

                }

                if (func == null)
                {
                    throw new HttpStatusAwareException(404, "Not Found");
                }

                if (func is Func<dynamic, HttpListenerResponse, string>)
                {
                    body = ((Func<dynamic, HttpListenerResponse, string>)func)(getJsonObj(ctx.Request), ctx.Response);
                }
                else
                {
                    body = ((Func<HttpListenerRequest, HttpListenerResponse, string>)func)(ctx.Request, ctx.Response);
                }
                reply(ctx.Response, body);
            }
            catch (HttpStatusAwareException ex)
            {
                reply(ctx.Response, ex);
            }
            catch (Exception ex)
            {
                reply(ctx.Response, new HttpStatusAwareException(500, "internal server error"));
            }
            finally
            {
            }
        }

        public static bool isRunning()
        {
            return !_stop || listner.IsListening || MainLoop != null;
        }

        public static void WaitOnServerToStop()
        {
            MainLoop.Wait();
        }

        public static void stop()
        {
            _stop = true;
        }

        public static void useHttp(bool use)
        {
            _isHttp = use;
        }

        private static void reply(HttpListenerResponse response, string body){
			
			byte[] outBuffer = null;
            response.StatusCode = 200;
            if (body != null){
				outBuffer = Encoding.ASCII.GetBytes(body);
				response.ContentLength64 = outBuffer.Length;
				response.OutputStream.Write(outBuffer, 0, outBuffer.Length);
			}

            response.OutputStream.Flush();
            response.OutputStream.Close();
            response.OutputStream.Dispose();
			response.Close();
		}

        private static void reply(HttpListenerResponse response, HttpStatusAwareException ex)
        {
            string body = ex.Body;
            response.StatusCode = ex.StatusCode;
            byte[] outBuffer = null;
            if (body != null)
            {
                outBuffer = Encoding.ASCII.GetBytes(body);
                response.ContentLength64 = outBuffer.Length;
                response.OutputStream.Write(outBuffer, 0, outBuffer.Length);
            }

            
            response.OutputStream.Flush();
            response.OutputStream.Close();
            response.OutputStream.Dispose();
            response.Close();
        }

        public static void port(string port){
			Server._port = port;
		}

        public static void port(int port)
        {
            Server._port = port.ToString();
        }

        public static Server getInstance(){
			if(server == null ){
				lock(sync){
					if(server == null){
						server = new Server();
					}
				}
			}
			return Server.server;
		}

        public static Server startServerInstance()
        {
            return getInstance(); 
        }

        /// <summary>
        /// Sets the path to the function to execute. 
        /// </summary>
        /// <param name="path"></param>
        /// <param name="func"></param>
        public static void post(string path, Func<HttpListenerRequest, HttpListenerResponse, string> func){

            if (server == null)
                throw new ServerNotStartedException();

            if (path != null && path.Length != 0)
				postMappings[path] = (object)func;
			
		}

        /// <summary>
        /// Sets the path to the function to execute.  The first variable of the function is the dynamic body.
        /// Only works with a valid JSON body to a dynamic variable that represents the body.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="func"></param>
        public static void post(string path, Func<dynamic, HttpListenerResponse, string> func)
        {

            if (server == null)
                throw new ServerNotStartedException();

            if (path != null && path.Length != 0)
                postMappings[path] = (object)func;

        }

        /// <summary>
        /// Sets the path to the function to execute. 
        /// </summary>
        /// <param name="path"></param>
        /// <param name="func"></param>
        public static void get(string path, Func<HttpListenerRequest, HttpListenerResponse, string> func){

			if(server == null)
                throw new ServerNotStartedException();

            if (path != null && path.Length != 0)
				getMappings[path] = (object)func;
			
		}

        /// <summary>
        /// Sets the path to the function to execute.  The first variable of the function is the dynamic body.
        /// Only works with a valid JSON body to a dynamic variable that represents the body.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="func"></param>
        public static void get(string path, Func<dynamic, HttpListenerResponse, string> func)
        {

            if (server == null)
                throw new ServerNotStartedException();

            if (path != null && path.Length != 0)
                getMappings[path] = (object)func;

        }

        /// <summary>
        /// Sets the path to the function to execute. 
        /// </summary>
        /// <param name="path"></param>
        /// <param name="func"></param>
        public static void put(string path, Func<HttpListenerRequest, HttpListenerResponse, string> func)
        {

            if (server == null)
                throw new ServerNotStartedException();

            if (path != null && path.Length != 0)
                putMappings[path] = (object)func;

        }

        /// <summary>
        /// Sets the path to the function to execute.  The first variable of the function is the dynamic body.
        /// Only works with a valid JSON body to a dynamic variable that represents the body.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="func"></param>
        public static void put(string path, Func<dynamic, HttpListenerResponse, string> func)
        {

            if (server == null)
                throw new ServerNotStartedException();

            if (path != null && path.Length != 0)
                putMappings[path] = (object)func;

        }

        /// <summary>
        /// Sets the path to the function to execute. 
        /// </summary>
        /// <param name="path"></param>
        /// <param name="func"></param>
        public static void delete(string path, Func<HttpListenerRequest, HttpListenerResponse, string> func)
        {

            if (server == null)
                throw new ServerNotStartedException();

            if (path != null && path.Length != 0)
                deleteMappings[path] = (object)func;

        }

        /// <summary>
        /// Sets the path to the function to execute.  The first variable of the function is the dynamic body.
        /// Only works with a valid JSON body to a dynamic variable that represents the body.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="func"></param>
        public static void delete(string path, Func<dynamic, HttpListenerResponse, string> func)
        {

            if (server == null)
                throw new ServerNotStartedException();

            if (path != null && path.Length != 0)
                deleteMappings[path] = (object)func;

        }

        public static string getBody(HttpListenerRequest request)
        {
            byte[] buffer = new byte[request.ContentLength64];

            request.InputStream.Read(buffer, 0, buffer.Length);

            return Encoding.UTF8.GetString(buffer, 0, buffer.Length);
        }

        public static dynamic getJsonObj(HttpListenerRequest request)
        {

            string json = getBody(request);

            if (json == null || json == "") json = "{}";

            return JObject.Parse(json);
        }

        public class HttpStatusAwareException : Exception
        {

            public HttpStatusAwareException(int statusCode, string message)
            {
                this.StatusCode = statusCode;
                this.Body = message;
            }

            public int StatusCode { get; set; }

            public string Body { get; set; }

        }
    }
}