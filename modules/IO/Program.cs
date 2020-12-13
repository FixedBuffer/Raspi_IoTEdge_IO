using System;
using System.Runtime.Loader;
using System.Device.Gpio;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport.Mqtt;

namespace IO
{
    class Program
    {
        static ModuleClient ioTHubModuleClient = null;

        static GpioController controller = null;
        // GPIO 6 which is physical pin 31
        static int outPin = 6;
        // GPIO 12 is physical pin 32
        static int inPin = 12;


        static async Task Main(string[] args)
        {
            var cts = new CancellationTokenSource();
            await InitAsync(cts.Token);
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            await WhenCancelledAsync(cts.Token);
        }

        /// <summary>
        /// Handles cleanup operations when app is cancelled or unloads
        /// </summary>
        public static Task WhenCancelledAsync(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        /// <summary>
        /// Initializes the ModuleClient and sets up the callback to receive
        /// messages containing temperature information
        /// </summary>
        static async Task InitAsync(CancellationToken cancellation)
        {
            InitGPIO();
            await InitModuleAsync(cancellation);
            _ = InitReadAsync(cancellation);
            _ = InitWriteAsync(cancellation);
        }

        static async Task InitReadAsync(CancellationToken cancellation)
        {
            while (!cancellation.IsCancellationRequested)
            {
                var messageBytes = Encoding.UTF8.GetBytes(controller.Read(inPin).ToString());
                using (var pipeMessage = new Message(messageBytes))
                {
                    await ioTHubModuleClient.SendEventAsync("output", pipeMessage);
                }
                await Task.Delay(1000, cancellation);
            }
        }

        static async Task InitWriteAsync(CancellationToken cancellation)
        {
            var currentValue = PinValue.High;
            while (!cancellation.IsCancellationRequested)
            {
                currentValue = currentValue == PinValue.High ? PinValue.Low : PinValue.High;
                controller.Write(outPin, currentValue);
                await Task.Delay(300, cancellation);
            }
        }

        static async Task InitModuleAsync(CancellationToken cancellation)
        {
            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            // Open a connection to the Edge runtime
            ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            await ioTHubModuleClient.OpenAsync(cancellation);
            Console.WriteLine("IoT Hub module client initialized.");
        }

        static void InitGPIO()
        {
            // Construct GPIO controller
            controller = new GpioController();

            // Sets the LED pin to output mode so we can switch something on
            controller.OpenPin(outPin, PinMode.Output);

            // Sets the button pin to input mode so we can read a value
            controller.OpenPin(inPin, PinMode.Input);
        }
    }
}
