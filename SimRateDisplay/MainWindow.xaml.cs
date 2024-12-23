using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using Microsoft.FlightSimulator.SimConnect;

namespace SRD.Gui
{
    public partial class MainWindow : Window
    {
        private SimConnect simConnect = null;
        private bool keepRunning = true;
        private const int WM_USER_SIMCONNECT = 0x0402;
        private ManualResetEvent resetEvent = new ManualResetEvent(false);

        private double currentSimRate = 1.0; // Current simulation rate

        // Define a structure to hold the simulation rate
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        struct SimRate
        {
            public double SimulationRate;
        }

        // Define enum for data request IDs
        enum DataRequestID
        {
            RequestSimulationRate
        }

        // Define enum for definition IDs
        enum DefinitionID
        {
            DefinitionSimulationRate
        }

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.Invoke(InitializeSimConnect);

            Thread simConnectThread = new Thread(ProcessSimConnectMessages);
            simConnectThread.IsBackground = true;
            simConnectThread.Start();
        }

        private void InitializeSimConnect()
        {
            try
            {
                var handle = new WindowInteropHelper(this).Handle;

                simConnect = new SimConnect("SimRateDisplay", handle, WM_USER_SIMCONNECT, null, 0);

                simConnect.OnRecvOpen += SimConnect_OnRecvOpen;
                simConnect.OnRecvQuit += SimConnect_OnRecvQuit;
                simConnect.OnRecvException += SimConnect_OnRecvException;
                simConnect.OnRecvSimobjectData += SimConnect_OnRecvSimobjectData;

                simConnect.AddToDataDefinition(
                    DefinitionID.DefinitionSimulationRate,
                    "SIMULATION RATE",
                    "number",
                    SIMCONNECT_DATATYPE.FLOAT64,
                    0.0f,
                    SimConnect.SIMCONNECT_UNUSED
                );
                simConnect.RegisterDataDefineStruct<SimRate>(DefinitionID.DefinitionSimulationRate);

                simConnect.RequestDataOnSimObject(
                    DataRequestID.RequestSimulationRate,
                    DefinitionID.DefinitionSimulationRate,
                    SimConnect.SIMCONNECT_OBJECT_ID_USER,
                    SIMCONNECT_PERIOD.SECOND, // Set period to second
                    SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT,
                    0, 0, 0
                );

                Dispatcher.Invoke(() => StatusText.Text = "Connected to Microsoft Flight Simulator!");
            }
            catch (COMException ex)
            {
                Dispatcher.Invoke(() =>
                {
                    StatusText.Text = "Error connecting to SimConnect: " + ex.Message;
                });
            }
        }

        private void ProcessSimConnectMessages()
        {
            while (keepRunning)
            {
                if (simConnect != null)
                {
                    simConnect.ReceiveMessage();
                }
                Thread.Sleep(1);
            }
            resetEvent.Set();
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            keepRunning = false;
            resetEvent.WaitOne();

            if (simConnect != null)
            {
                simConnect.Dispose();
                simConnect = null;
            }
        }

        private void SimConnect_OnRecvOpen(SimConnect sender, SIMCONNECT_RECV_OPEN data)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = "Connected to Microsoft Flight Simulator!";
            });
        }

        private void SimConnect_OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = "Simulator has exited.";
            });
            keepRunning = false;
        }

        private void SimConnect_OnRecvException(SimConnect sender, SIMCONNECT_RECV_EXCEPTION data)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = "SimConnect Exception: " + data.dwException;
            });
        }

        private void SimConnect_OnRecvSimobjectData(SimConnect sender, SIMCONNECT_RECV_SIMOBJECT_DATA data)
        {
            if (data.dwRequestID == (uint)DataRequestID.RequestSimulationRate)
            {
                SimRate simRate = (SimRate)data.dwData[0];
                currentSimRate = simRate.SimulationRate;

                Dispatcher.Invoke(() =>
                {
                    StatusText.Text = $"Current Simulation Rate: {currentSimRate:F2}";
                });
            }
        }
    }
}
