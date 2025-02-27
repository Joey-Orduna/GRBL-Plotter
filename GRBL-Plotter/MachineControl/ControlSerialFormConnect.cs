﻿/*  GRBL-Plotter. Another GCode sender for GRBL.
    This file is part of the GRBL-Plotter application.
   
    Copyright (C) 2015-2022 Sven Hasemann contact: svenhb@web.de

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

/* 2022-01-09 split file for selection of serial or ethernet
*/

using System;
using System.IO;
using System.IO.Ports;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GrblPlotter
{
    public partial class ControlSerialForm : Form        // Form can be loaded twice!!! COM1, COM2
    {

        private bool IsConnectedToGrbl()
        {
            if (!CbEthernetUse.Checked) { return serialPort.IsOpen; }
            else { return Connected; }
        }

        /***** ethernet preperations *****/

        private NetworkStream Connection;
        TcpClient ClientEthernet;
        bool Connected = false;
        StreamReader reader;
        //     StreamWriter writer;

        public void ConnectToGrbl()
        { ConnectToGrbl(null, null); }
        public void ConnectToGrbl(object sender, EventArgs e)//, bool showMessageBox = true)
        {
            bool showMessageBox = false;	//true;
            rxErrorCount = 0;
			tryDoSerialConnection = true;
			
            if (!CbEthernetUse.Checked)
            {
                AddToLog("\nTry to connect to serial " + cbPort.Text + " @ " + cbBaud.Text);
                Application.DoEvents();
                OpenPortSerial();
                if (serialPort.IsOpen)
                {
                    CbEthernetUse.Enabled = false;
                    Connected = true;
                    tryDoSerialConnection = false;
                }
            }
            else
            {
                try
                {
                    Logger.Info("==== Connecting to {0}:{1} ====", TbEthernetIP.Text, TbEthernetPort.Text);
                    AddToLog("\nTry to connect to Ethernet (wait 20 sec.)\n" + TbEthernetIP.Text + ":" + TbEthernetPort.Text);
					timerSerial.Interval = 1000;
                    timerSerial.Start();

                    if (int.TryParse(TbEthernetPort.Text, out int port))
                    {
                        if ((port >= 0) && (port <= 65535))
                        {
                            BtnOpenPortEthernet.Text = Localization.GetString("serialClose");
                            BtnOpenPortEthernet.Enabled = false;
                            CbEthernetUse.Enabled = false;
                            Application.DoEvents();
                            ClientEthernet = new TcpClient(TbEthernetIP.Text, port);
                            Connected = true;
                            Connection = ClientEthernet.GetStream();
							tryDoSerialConnection = false;

                            SaveSettings();
                            reader = new StreamReader(Connection);
                            AddToLog("Connect to ethernet " + TbEthernetIP.Text + ":" + TbEthernetPort.Text);

							ConnectionSucceed("Connect to ethernet " + TbEthernetIP.Text + ":" + TbEthernetPort.Text);

                            timerSerial.Interval = 500;       		// timerReload;

                            CbEthernetUse.Enabled = false;
                            BtnOpenPortEthernet.Enabled = true;
                            Application.DoEvents();
                        }
                        else
                        {
							countMinimizeForm = 0;
                            string msg = string.Format("Port number must be between 0 and 65535: {0}");
                            Logger.Error(msg);
                            AddToLog(msg);
                            if (showMessageBox) MessageBox.Show(msg, "Error");
                        }
                    }
                    else
                    {
						countMinimizeForm = 0;
                        string msg = string.Format("Port is not a valid number: {0}", TbEthernetPort.Text);
                        Logger.Error(msg);
                        AddToLog(msg);
                        if (showMessageBox) MessageBox.Show(msg, "Error");
                    }
                }
                catch (ArgumentNullException)
                {
					countMinimizeForm = 0;
                    string msg = "ArgumentNullException - Invalid address or port";
                    Logger.Error(msg);
                    AddToLog(msg);
                    if (showMessageBox) MessageBox.Show(msg, "Error");
                    CbEthernetUse.Enabled = true;
                    BtnOpenPortEthernet.Enabled = true;
                    BtnOpenPortEthernet.Text = Localization.GetString("serialOpen");
                    Connected = false;
                }
                catch (SocketException)
                {
					countMinimizeForm = 0;
                    string msg = "SocketException - Connection failure";
                    Logger.Error(msg);
                    AddToLog(msg);
                    if (showMessageBox) MessageBox.Show(msg, "Error");
                    CbEthernetUse.Enabled = true;
                    BtnOpenPortEthernet.Enabled = true;
                    BtnOpenPortEthernet.Text = Localization.GetString("serialOpen");
                    Connected = false;
                }
            }
			UpdateControls();
        }
		
        public void DisconnectFromGrbl(object sender, EventArgs e)
        {
			tryDoSerialConnection = false;
            Connected = false;
         //   reader = null;
            //     writer = null;
            CbEthernetUse.Enabled = true;

            if (!CbEthernetUse.Checked)
            {
                ClosePortSerial();
				BtnOpenPortSerial.Text = Localization.GetString("serialOpen");
            }
            else
            {
                if (Connection != null)
                {
                    Connection.Flush();
                    Connection.Close();
                    Connection.Dispose();
                    reader.Close();
                    ClientEthernet.Close();
                    AddToLog("==== Disconnected from Ethernet ====");
                }
                timerSerial.Interval = 1000;
                //SaveSettings();
                Connection = null;
                BtnOpenPortEthernet.Text = Localization.GetString("serialOpen");
            }
            if (iamSerial == 1) { Grbl.isConnected = SerialPortOpen = IsConnectedToGrbl(); }
            OnRaisePosEvent(new PosEventArgs(posWork, posMachine, GrblState.unknown, machineState, mParserState, ""));// lastCmd));
            UpdateControls();
        }
		
		private void ConnectionSucceed(string msg)
		{
			BtnOpenPortSerial.Text = Localization.GetString("serialClose");  // "Close";
			isDataProcessing = true;
			if (iamSerial == 1)
			{	Grbl.isConnected = IsConnectedToGrbl();
				Grbl.lastMessage = msg;
				Grbl.Clear();			// reset internal grbl variables
			}

			if (Properties.Settings.Default.serialMinimize)
				countMinimizeForm = (int)(3000 / timerSerial.Interval); 	// minimize window after 5 sec.

			timerSerial.Interval = Grbl.pollInterval;       		// timerReload;
			countMissingStatusReport = (int)(2000 / timerSerial.Interval);
			timerSerial.Enabled = true;
			serialPortError = false;

			countPreventOutput = 0; countPreventEvent = 0;
			IsHeightProbing = false;
			if (Grbl.grblSimulate)
			{
				Grbl.grblSimulate = false;
				AddToLog("* Stop simulation\r\n");
			}
			GrblReset(false);   		// reset controller, don't savePos, wait for reset response

			OnRaisePosEvent(new PosEventArgs(posWork, posMachine, GrblState.unknown, machineState, mParserState, ""));// lastCmd));					
		}
    }
}
