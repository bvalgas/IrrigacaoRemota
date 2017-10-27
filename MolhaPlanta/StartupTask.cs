using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using Windows.ApplicationModel.Background;
using Windows.System.Threading;
using Windows.Devices.Gpio;
using Windows.Devices.Spi;
using MolhaPlanta.Class;
using System.Threading.Tasks;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Net;
using System.IO;


// The Background Application template is documented at http://go.microsoft.com/fwlink/?LinkID=533884&clcid=0x409

namespace MolhaPlanta
{
    public sealed class StartupTask : IBackgroundTask
    {
        BackgroundTaskDeferral _deferral;
        GpioPin pin;
        ThreadPoolTimer timer;
        ThreadPoolTimer timer2;
        SpiDevice SpiADC;
        DateTime UltimaMolhada = DateTime.Now;
        private byte PINO_SLIDER=0x80;
        int adcValue;
        public void Run(IBackgroundTaskInstance taskInstance)
        {
            InitGPIO();
            InitSPI();
            _deferral = taskInstance.GetDeferral();

            timer = ThreadPoolTimer.CreatePeriodicTimer(Timer_Tick, TimeSpan.FromMilliseconds(10000));
            timer2 = ThreadPoolTimer.CreatePeriodicTimer(Timer_Tick2, TimeSpan.FromMilliseconds(10000));
        }

        private void Timer_Tick(ThreadPoolTimer timer)
        {

            var httpWebRequest = (HttpWebRequest)WebRequest.Create("http://serverless-study.appspot.com/api/v1/irrigacoes");
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "GET";

            var text = "";
            var httpResponse = httpWebRequest.GetResponseAsync().Result;
            using (var sr = new StreamReader(httpResponse.GetResponseStream()))
            {
                text = sr.ReadToEnd();
            }


            if (text.Contains("\"Irrigar\":1") && pin.Read() == GpioPinValue.Low)
            {
                pin.Write(GpioPinValue.High);
                UltimaMolhada = DateTime.Now;
            }
            else if((DateTime.Now - UltimaMolhada).Seconds >= 30)
            {
                pin.Write(GpioPinValue.Low);
            }
        }

        private void Timer_Tick2(ThreadPoolTimer timer)
        {
            var valor = LerADC(PINO_SLIDER).ToString();
            var httpWebRequest = (HttpWebRequest)WebRequest.Create("http://serverless-study.appspot.com/api/v1/umidades");
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "POST";
            var dataE = httpWebRequest.GetRequestStreamAsync();
            using (var streamWriter = new StreamWriter(dataE.Result))
            {
                string json = "{\"valor\":\""+valor+"\"}";

                streamWriter.Write(json);
                streamWriter.Flush();
                //streamWriter.Dispose();
            }

            var httpResponse = httpWebRequest.GetResponseAsync();

        }

        public int ConvertToInt([ReadOnlyArray] byte[] data)
        {
            int result = 0;

            result = data[1] & 0x03;
            result <<= 8;
            result += data[2];
            return result;
        }

        public int LerADC(byte canal)                              // Método para ler o ADC na porta especificada
        {
            byte[] readBuffer = new byte[3];                        // Buffer para receber os dados
            byte[] writeBuffer = new byte[3] { 0x00, 0x00, 0x00 };

            writeBuffer[0] = 0x01;
            writeBuffer[1] = canal;                                 // Seleciona qual canal do ADC irá ser lido
            SpiADC.TransferFullDuplex(writeBuffer, readBuffer);     // Lê os dados do ADC             
            adcValue = ConvertToInt(readBuffer);                    // Converte os valores em Inteiro 
            return adcValue;
        }
        private async Task InitSPI()
        {
            try
            {
                var settings = new SpiConnectionSettings(0);    // Seleciona a porta SPI0 da DragonBoard
                settings.ClockFrequency = 500000;               // Configura o clock do barramento SPI em 0.5MHz 
                settings.Mode = SpiMode.Mode0;                  // COnfigura polaridade e fase do clock do SPI
                var controller = await SpiController.GetDefaultAsync();
                SpiADC = controller.GetDevice(settings);
            }
            catch (Exception ex)
            {
                throw new Exception("Falha na inicialização do SPI", ex);
            }
        }
        private void InitGPIO()
        {
            var gpio = GpioController.GetDefault();
            
            if (gpio == null)
            {
                pin = null;
                return;
            }

            pin = gpio.OpenPin(36);

            if (pin == null)
            {
                return;
            }

            pin.Write(GpioPinValue.High);
            pin.SetDriveMode(GpioPinDriveMode.Output);
        }
    }
}
