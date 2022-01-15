using System;
using System.Collections.Generic;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Ports;
using System.IO;


namespace WindowsFormsApp5
{
    public partial class Form1 : Form
    {
        //*******************************************************************************************************************************
        enum PackageType { None, L, I, F, R };
        PackageType waitingAckFor = PackageType.None;
        PackageType WaitingAckFor
        {
            get { return waitingAckFor; }
            set
            {
                waitingAckFor = value;
                if (value != PackageType.None)
                {
                    timeout_timer = new Timer();
                    timeout_timer.Interval = 2000;
                    timeout_timer.Tick += (object sender_obj, EventArgs ea) =>
                    {
                        SendRet();
                        timeout_timer.Stop();
                    };
                    timeout_timer.Start();
                }
            }
        }
        bool isLinked;
        bool IsLinked
        {
            get { return isLinked; }
            set
            {
                isLinked = value;
                string dtn = DateTime.Now.ToLongTimeString();
                if (value)
                {
                    label8.Text = "активно";
                    label8.ForeColor = Color.Green;
                    button2.Enabled = true; // поменять возможность нажатия кнопок соединения
                    button1.Enabled = false;
                    textBox2.ReadOnly = false;
                    button3.Enabled = true; // добавить возможность нажатия кнопки Send Message
                    textBox1.AppendText("[" + dtn + "] Системное сообщение: Соединение установлено\n");
                    
                }
                else
                {
                    this.label8.Text = "не активно";
                    this.label8.ForeColor = Color.Red;
                    button2.Enabled = false; // поменять возможность нажатия кнопок соединения
                    button1.Enabled = true;
                    textBox2.ReadOnly = true;
                    button3.Enabled = false; // убрать возможность нажатия кнопки Send message
                    textBox1.AppendText("[" + dtn + "] Системное сообщение: Соединение разорвано\n"); // вывод в консоль сообщение о разрыве связи
                    
                }
            }
        }
        Timer link_timer, timeout_timer;
        //*******************************************************************************************************************************
        public Form1()
        {
            InitializeComponent();
            this.label8.Text = "не активно";
            this.label8.ForeColor = Color.Red;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            string[] ports = SerialPort.GetPortNames(); // Получаем список портов
            comboBox1.Items.AddRange(ports); // Добавляем в список возможные COM порты
        }

        private void button1_Click(object sender, EventArgs e) // Кнопка соединения
        {
            button1.Enabled = false;
            String port = comboBox1.Text; // Выбор порта
            int baudrate = Convert.ToInt32(9600); // ComboBox2.Text
            Parity parity = (Parity)Enum.Parse(typeof(Parity), "None"); // ComboBox3.Text
            int databits = Convert.ToInt32(8); // ComboBox4.Text
            StopBits stopbits = (StopBits)Enum.Parse(typeof(StopBits), "One"); // ComboBox5.Text
            serialport_connect(port, baudrate, parity, databits, stopbits); // Соединение
            button2.Enabled = true;
        }

        byte[] dataForSending = new byte[0];

        private void button3_Click(object sender, EventArgs e) // Кнопка отправки текстового сообщения
        {
            if (textBox2.Text != "")
            {
                if (textBox2.Lines[0] == "")
                    for (int i = 0; i < 5; i++)
                    {
                        try
                        {
                            textBox2.Lines[i] = textBox2.Lines[i + 1];
                        }
                        catch (Exception ex)
                        { }
                    }
            }
                
            DateTime dt = DateTime.Now; // Дата
            String dtn = dt.ToLongTimeString(); // Дата

            if (textBox2.Text != "") // Если строка сообщения для отправки не пустая
            {
                try
                {
                    textBox1.AppendText("[" + dtn + "] Sent: " + textBox2.Text + "\n"); // Вывод в консоль сообщения об отправке
                    dataForSending = Encode(Pack(ToBits(textBox2.Text), 'I')); // Перевод текстового сообщения в байты, упаковка байтов в кадр, кодирование кадра циклическим кодом
                    serial_port.RtsEnable = false;
                    serial_port.Write(dataForSending, 0, dataForSending.Length); // Отправка по COM порту закодированного кадра
                    serial_port.RtsEnable = true;
                    WaitingAckFor = PackageType.I; // Ожидание кадра подтверждения получения
                    textBox2.Lines = null; // Очищение строки ввода текстовых сообщений
                    if (checkBox1.Checked == true)
                    {
                        // Создание переменной для проверки отправленного (исключительно для демонстрации)
                        string buffer = "";
                        for (int i = 0; i < dataForSending.Length; i++)
                        {
                            var byteString = Convert.ToString(dataForSending[i], 2);
                            var condition = (8 - byteString.Length);
                            for (int j = 0; j < condition; j++)
                                byteString = "0" + byteString;

                            buffer += byteString;
                        }
                        Data.Value = buffer;
                        Form3 f = new Form3();
                        f.ShowDialog();
                        // Создание переменной для проверки отправленного (исключительно для демонстрации)
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
            }
        }

        public static class Data
        {
            public static string Value { get; set; }
        }

        public System.IO.Ports.SerialPort serial_port;
        public void serialport_connect(String port, int baudrate, Parity parity, int databits, StopBits stopbits)  // Срабатывает при подсоединении с другой стороны
        {
            try
            {
                serial_port = new System.IO.Ports.SerialPort(port, baudrate, parity, databits, stopbits);
                serial_port.Open(); // инициализируем и открываем порт

                textBox1.AppendText("[" + DateTime.Now.ToLongTimeString() + "] Системное сообщение: Порт открыт\n"); // вывод в консоль системного сообщения

                serial_port.DtrEnable = true;
                serial_port.RtsEnable = true;
                serial_port.ReadTimeout = 500;
                serial_port.WriteTimeout = 500;
                if (serial_port.DsrHolding && serial_port.CtsHolding)
                {
                    IsLinked = true;
                }
                else
                {
                    link_timer = new Timer();
                    link_timer.Interval = 100;
                    link_timer.Tick += (object sender_obj, EventArgs ea) =>
                    {
                        if (serial_port.DsrHolding && serial_port.CtsHolding)
                        {
                            IsLinked = true;
                            link_timer.Stop();
                        }
                    };
                    link_timer.Start();
                }

                serial_port.DataReceived += new SerialDataReceivedEventHandler(sport_DataReceived);
            }
            catch (Exception ex) { MessageBox.Show(ex.ToString(), "Error"); }
        }

        private void button2_Click(object sender, EventArgs e) // Оборвать соединение
        {
            button2.Enabled = false;
            DateTime dt = DateTime.Now;
            String dtn = dt.ToLongTimeString();

            if (serial_port.IsOpen)
            {
                if ((link_timer != null) && (link_timer.Enabled))
                    link_timer.Stop();

                SendUplink(); // Отправить кадр о закрытии соединения
                serial_port.Close(); // Закрыть порт
                if (IsLinked)
                    IsLinked = false;
                textBox1.AppendText("[" + dtn + "] Системное сообщение: Порт закрыт\n");
            }
            button1.Enabled = true;
        }

        //**********************************************************************************ПОЛУЧЕНИЕ ДАННЫХ ИЗ COM ПОРТА ********************************************************

        byte[] bytes_test; // байты загруженного (с компьютера) файла
        string ext = ""; // расширение текущего загруженного файла
        string file_title = ""; // название текущего загруженного файла
        byte[] bytes_test_rec; // аналогичные массивы для принятого файла
        string ext_rec = "";
        string file_title_rec = "";
        string file_name = ""; // название файла целиком с расширением
        List<byte> file_string = new List<byte>(); // лист байтов для собирания файла
        int pack_amount_received = 0; // сколько пакетов файла осталось получить
        int pack_itr_rec = 0; // итерация полученных пакетов
        bool name_receiving = false; // как воспринимать следующий информационный кадр, как сообщение или как название файла. False - как сообщение
        private object sync_temp = new object();
        private void sport_DataReceived(object sender, SerialDataReceivedEventArgs e) // Срабатывает, когда получает информацию по COM порту
        {
            DateTime dt = DateTime.Now;
            String dtn = dt.ToLongTimeString();
            byte[] ReceivedMessage = new byte[0]; // Полученный кадр
            byte[] UnpackedReceivedMessage = new byte[0];
            string message_out = ""; // То, что выведется в консоль
            bool end_reading = false; // Заканчивать ли while цикл, нужно для получения файлов. Переводится в true, чтобы прекратить считывание информации с порта
            bool show_message = false; // Выводить ли что-то в консоль в конце исполнения функции
            do
            {
                ReceivedMessage = ReadFromPort(); // Считывание информации из порта
                try
                {
                    ReceivedMessage = Decoding(ReceivedMessage); // Декодирование по циклическому коду
                    UnpackedReceivedMessage = Unpack(ReceivedMessage);
                }
                catch
                {
                    serial_port.ReadExisting();
                    SendRet();
                    WaitingAckFor = PackageType.R;
                }
                switch (Convert.ToChar(ReceivedMessage[1])) // Проверка типа кадра
                {
                    case 'I': // Информационный
                        if (name_receiving) // Если это последний пакет с названием файла
                        {
                            file_name = Encoding.UTF8.GetString(UnpackedReceivedMessage); // Распаковка кадра для получения название
                            label10.Text = file_name; // Вывод названия файла в "название текущего принятого файла", далее вывод названия полученного файла в консоль
                            message_out = file_name;
                            ext_rec = file_name.Substring(file_name.IndexOf(".")); // Запись расширения
                            file_title_rec = file_name.Substring(0, file_name.IndexOf(".")); // Запись названия
                            bytes_test_rec = file_string.ToArray(); // Запись принятого файла
                            file_string = new List<byte>(); // Аннулирование листа для принятого файла
                            name_receiving = false; // Сброс в режим получения обычных сообщений
                            button6.Enabled = true;
                        }
                        else
                        {
                            message_out = Encoding.UTF8.GetString(UnpackedReceivedMessage); // То, что выведется в консоль, так как это обычное сообщение
                        }
                        show_message = true; // В конце функции в консоль что-то выведется
                        end_reading = true; // Закончить цикл While
                        SendACK(); // Отправить кадр ACK
                        break;

                    case 'L':
                        if (!isLinked) // Если соеденение ещё не установлено
                        {
                            SendACK(); // Отправить кадр ACK
                            textBox1.AppendText("[" + dtn + "] " + "Системное сообщение: Соединение установлено по запросу\n"); // Вывести в консоль сообщение об успешном соединении
                        }
                        end_reading = true; // Закончить цикл
                        break;

                    case 'U':
                        button2_Click(null, null); // Отключиться
                        end_reading = true; // Закончить цикл
                        break;

                    case 'R':
                        switch(WaitingAckFor)
                        {
                            case PackageType.F:
                                SendingFile(bytes_test);
                                break;
                            case PackageType.I:
                                try
                                {
                                    serial_port.RtsEnable = false;
                                    serial_port.Write(dataForSending, 0, dataForSending.Length); // Отправка по COM порту закодированного кадра
                                    serial_port.RtsEnable = true;
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show("Ошибка при переотправке сообщения");
                                }
                                break;
                            default:
                                SendACK();
                                break;
                        }
                         // При получении, отправить файл снова
                        end_reading = true; // Закончить цикл
                        break;

                    case 'A':
                        switch (WaitingAckFor) // В зависимости от ожидаемого ACK вывести нужное сообщение
                        {
                            case PackageType.L:
                                if (!isLinked)
                                {
                                    isLinked = true;
                                    textBox1.AppendText("[" + dtn + "] " + "System message: Соединение установлено по отправленному запросу\n");
                                }
                                break;
                            case PackageType.I:
                                if (checkBox1.Checked)
                                    MessageBox.Show("Пришел ACK в ответ на 'I' фрейм");
                                break;
                            case PackageType.F:
                                if (checkBox1.Checked)
                                    MessageBox.Show("Пришел ACK в ответ на 'F' фрейм");
                                break;
                            case PackageType.R:
                                if (checkBox1.Checked)
                                    MessageBox.Show("Пришел ACK в ответ на 'R' фрейм");
                                break;
                        }
                        WaitingAckFor = PackageType.None; // Больше не ожидается ACK
                        if (timeout_timer.Enabled)
                            timeout_timer.Stop();
                        end_reading = true; // Закончить цикл
                        break;

                    case 'F':
                        pack_amount_received = Convert.ToInt32(ReceivedMessage[3]); // Записать количество ожидаемых пакетов (из 3-го байта полученного кадра)
                        pack_itr_rec = 0; // Сбросить итерацию
                        file_string.AddRange(UnpackedReceivedMessage); // Добавить в сборку файла распакованное содержимое кадра
                        if (pack_amount_received == 0) // Если других пакетов не ожидается, следующим ждать информационный кадр
                            name_receiving = true;
                        break;

                    case 'C':
                        pack_amount_received--; // Пакет пришёл, убавить количество ожидаемых на 1
                        if (pack_itr_rec != Convert.ToInt32(ReceivedMessage[3])) // Проверяем итерацию, если ошибка, сбросить её, сбросить кол. ожидаемых пакетов и отправить кадр RET
                        {
                            pack_itr_rec = 0;
                            pack_amount_received = 0;
                            SendRet();
                            end_reading = true;
                            break;
                        }
                        pack_itr_rec++; // Итерация + 1
                        file_string.AddRange(UnpackedReceivedMessage); // Добавляем к сборке файла распакованное содержимое кадра
                        if (pack_amount_received == 0) // Если это последний пакет, ждать название файла
                            name_receiving = true;
                        break;
                }
            } while (end_reading == false);

            if (show_message)
                textBox1.AppendText("[" + dtn + "] " + "Received: " + message_out + "\n"); // Вывод в консоль, если условия, указанные выше, позволяют
        }
        //********************************************************************************************************************************************************************

        public void textBox2_TextChanged(object sender, EventArgs e) // EventArgs e
        {
            textBox3.Text = textBox2.Text.Length.ToString(); // Указывает текущее количество символов сообщения
        }
        
        private void button4_Click(object sender, EventArgs e) // Закрыть программу
        {
            this.Close();
        }

        private void button5_Click(object sender, EventArgs e) // Загрузить файл в программу с компьютера
        {
            openFileDialog1.Filter = "All files(*.*)|*.*"; // Text files(*.txt)|*.txt|
            openFileDialog1.FileName = "";
            if (openFileDialog1.ShowDialog() == DialogResult.Cancel) // Получаем выбранный файл
                return;
            string file_name = openFileDialog1.FileName; // Читаем файл в строку
            bytes_test = GetBinaryFile(file_name); // Конвертируем в массив байтов
            ext = Path.GetExtension(file_name); // Получаем расширение и название файла
            file_title = Path.GetFileName(file_name);
            label9.Text = file_title; // Обновляем название текущего файла
            MessageBox.Show("Файл успешно загружен в программу");
            button7.Enabled = true; // Теперь можно нажать кнопку отправить файл
        }

        private void button6_Click(object sender, EventArgs e) // Сохраняем полученный файл
        {
            saveFileDialog1.Filter = "Полученный файл|" + ext_rec;
            saveFileDialog1.FileName = file_title_rec;

            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                ByteArrayToFile(saveFileDialog1.FileName, bytes_test_rec); // Превращаем массив байт собранного файла в файл и сохраняем
            }
        }

        public static int pack_amount_write = 0; // Количество пакетов для отправки
        public static int pack_itr = 0; // Итерация пакетов
        private void button7_Click_1(object sender, EventArgs e) // Отправка файла
        {
                try
                {
                    SendingFile(bytes_test);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }
        }

        //**************************************************************************************************************************
        public void SendingFile(byte[] data) // Отправка файла
        {
            byte[] byte_full_pack = data; // Заполняем локальную переменную
            if (byte_full_pack.Length > 32500) // Слишком большой файл - вывод сообщения и отмена отправки
            {
                MessageBox.Show("File is too large, sending cancelled");
            }
            else
            {
                int pack_amount = (byte_full_pack.Length / 255) - 1;
                if (byte_full_pack.Length % 255 > 0)
                    pack_amount++;
                pack_amount_write = pack_amount;
                byte[] byte_pack = new byte[Math.Min(byte_full_pack.Length, 255)];
                for (int i = 0; i < Math.Min(byte_full_pack.Length, 255); i++)
                    byte_pack[i] = byte_full_pack[i];
                byte[] sent_data = Encode(Pack(byte_pack, 'F'));
                serial_port.RtsEnable = false;
                serial_port.Write(sent_data, 0, sent_data.Length);
                serial_port.RtsEnable = true;

                for (int i = 0; i < pack_amount; i++)
                {
                    int len = 0;
                    pack_itr = i;
                    if (pack_amount == i + 1)
                        len = byte_full_pack.Length % 255;
                    else
                        len = 255;
                    byte_pack = new byte[len];
                    for (int j = 0; j < len; j++)
                        byte_pack[j] = byte_full_pack[j + 255 * (i + 1)];
                    sent_data = Encode(Pack(byte_pack, 'C'));
                    serial_port.RtsEnable = false;
                    serial_port.Write(sent_data, 0, sent_data.Length);
                    serial_port.RtsEnable = true;
                }
                byte[] name_to_send = Encode(Pack(ToBits(file_title), 'I'));
                serial_port.RtsEnable = false;
                serial_port.Write(name_to_send, 0, name_to_send.Length);
                serial_port.RtsEnable = true;
                DateTime dt = DateTime.Now; // Дата
                String dtn = dt.ToLongTimeString(); // Дата
                textBox1.AppendText("[" + dtn + "] " + "Sent: " + file_title + "\n");
                WaitingAckFor = PackageType.F;
            }
        }

        public byte[] ReadFromPort()
        {
            if (serial_port.IsOpen)
            {
                int bytes = serial_port.BytesToRead;
                byte[] start_buffer = new byte[6];
                if (bytes > 5) {
                    serial_port.Read(start_buffer, 0, 6);
                }
                else throw new Exception("ReadFromPort() exception: not enough frames to read, less than 3");
                byte[] start_buffer_dec = Decoding(start_buffer);
                char t_ch = Convert.ToChar(start_buffer_dec[1]);
                if (t_ch == 'I' || t_ch == 'F' || t_ch == 'C' )
                {
                    int len = Convert.ToInt32(start_buffer_dec[2]);
                    byte[] buffer = new byte[len*2 + 2 + ((t_ch != 'I') ? 2 : 0)];
                    List<byte> result_buffer = new List<byte>(start_buffer);
                    serial_port.Read(buffer, 0, len * 2 + 2 + ((t_ch != 'I') ? 2 : 0));
                    result_buffer.AddRange(buffer);
                    return result_buffer.ToArray();
                }
                else
                {
                    return start_buffer;
                }
            }
            else return new byte[0];
        }

        private byte[] GetBinaryFile(string filename)
        {
            byte[] bytes;
            using (FileStream file = new FileStream(filename, FileMode.Open, FileAccess.Read))
            {
                bytes = new byte[file.Length];
                for (int i = 0; i < file.Length; i++)
                {
                    bytes[i] = Convert.ToByte(file.ReadByte());
                }
            }
            return bytes;
        }
        public bool ByteArrayToFile(string fileName, byte[] byteArray)
        {
            try
            {
                using (var fs = new FileStream(fileName, FileMode.Create, FileAccess.Write))
                {
                    fs.Write(byteArray, 0, byteArray.Length);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception caught in process: {0}", ex);
                return false;
            }
        }

        // Непосредственно расшифровка последовательности из 7 символов циклическим кодом в последовательность из 4 с проверкой на ошибки
        public static byte[] Decode(byte[] input)
        {
            byte[] outputReg = new byte[7];
            byte[] r0 = new byte[2], r1 = new byte[2], r2 = new byte[2];
            r0[1] = input[6];
            byte a = 0;

            for (int i = 6; i >= 0; i--)
            {
                outputReg[i] = Convert.ToByte(a);
                if (i > 0)
                {
                    a = r2[1];
                    r2[1] = r1[1];
                    r1[1] = Convert.ToByte((r0[1] + a) % 2);
                    r0[1] = Convert.ToByte((input[i - 1] + a) % 2);
                }
            }

            byte[] message = new byte[] { outputReg[0], outputReg[1], outputReg[2], outputReg[3] };
            byte[] error = new byte[] { r0[1], r1[1], r2[1] };

            Console.WriteLine("Error: " + error[2].ToString() + error[1].ToString() + error[0].ToString());

            if ((error[0] != 0) || (error[1] != 0) || (error[2] != 0))
            {
                byte[,] table = new byte[,] { { 0, 0, 1 }, { 0, 1, 0 }, { 1, 0, 0 }, { 0, 1, 1 }, { 1, 1, 0 }, { 1, 1, 1 }, { 1, 0, 1 }, };

                int i;
                for (i = 0; i < 7; i++)
                {
                    if ((error[0] != table[i, 2]) || (error[1] != table[i, 1]) || (error[2] != table[i, 0])) // Don't touch it
                        continue;
                    else
                        break;
                }

                byte[] correctInput = input;
                correctInput[i] = Convert.ToByte((correctInput[i] + 1) % 2);
                return Decode(correctInput);
            }
            else
            {
                return message;
            }
        }

        // Кодирование методом умножения на порождающий полином 1011 с помощью линеной переключательной схемы.
        public static byte[] Encode(byte[] input)
        {
            List<byte> output_result = new List<byte>();
            for (int i = 0; i < input.Length; i++)
            {
                string binaryStr = Convert.ToString(input[i], 2);
                var condition = (8 - binaryStr.Length);
                for (int j = 0; j < condition; j++)
                    binaryStr = "0" + binaryStr;

                for (int m = 0; m < 2; m++)
                {
                    byte[] part_input = new byte[] { Convert.ToByte(binaryStr.Substring(m*4, 1), 2), Convert.ToByte(binaryStr.Substring(m * 4 + 1, 1), 2),
                        Convert.ToByte(binaryStr.Substring(m * 4 + 2, 1), 2), Convert.ToByte(binaryStr.Substring(m * 4 + 3, 1), 2) };
                    List<byte> inputReg = part_input.ToList();
                    List<byte> outputReg = new List<byte>();
                    inputReg.AddRange(new byte[] { 0, 0, 0 });
                    byte[] regB = new byte[2], regC = new byte[2], regD = new byte[2]; // regB[0] - last, regB[1] - current
                    byte[] outputReg_part = new byte[7];

                    for (int k = 0; k < 7; k++)
                    {
                        outputReg_part[k] = Convert.ToByte((inputReg[k] + regB[1] + regD[1]) % 2);

                        regD[1] = regC[1];
                        regC[1] = regB[1];
                        regB[1] = inputReg[k];
                    }

                    string buffer = "";
                    for (int j = 0; j < 7; j++)
                        buffer = Convert.ToString(outputReg_part[j]) + buffer;
                    
                    output_result.Add(Convert.ToByte(buffer, 2));
                }
            }

            return output_result.ToArray();
        }

        public static byte[] ToBits(string stream)
        {
            byte[] ByteCode = Encoding.UTF8.GetBytes(stream);
            return ByteCode;
        }

        public static byte[] Decoding(byte[] flow)
        {
            string binaryStr = "";
            for (int i = 0; i < flow.Length; i++)
            {
                string binaryStrTemp = Convert.ToString(flow[i], 2);
                var condition = (7 - binaryStrTemp.Length);
                for (int j = 0; j < condition; j++)
                    binaryStrTemp = "0" + binaryStrTemp;
                binaryStr += binaryStrTemp;
            }
            byte[] new_output = new byte[binaryStr.Length];
            for (int i = 0; i < binaryStr.Length; i++)
            {
                new_output[i] = Convert.ToByte(binaryStr.Substring(i, 1), 2);
            }
            int len = new_output.Length;
            byte[] arr = new_output;
            byte[] res = new byte[8];
            byte[] first = new byte[4];
            byte[] next_put = new byte[4];
            List<byte> final_res = new List<byte>();
            for (int i = 0; i < len / 14; i++)
            {
                for (int k = 0; k < 2; k++)
                {
                    byte[] next_dec = new byte[7];
                    for (int j = 0; j < 7; j++)
                    {
                        next_dec[j] = arr[(i * 14) + (k * 7) + (6 - j)];
                    }
                    next_put = Decode(next_dec);
                    if (k == 0)
                        first = next_put;
                }
                for (int k = 0; k < 4; k++)
                {
                    res[k] = first[k];
                    res[k + 4] = next_put[k];
                }

                int m = 1;
                int dec = 0;
                for (int k = 7; k >= 0; k--)
                {
                    dec += ((Convert.ToInt32(res[k])) * m);
                    m *= 2;
                }

                final_res.Add((byte)dec);
            }

            return final_res.ToArray();
        }

        // Упаковывает byte array длиной <= 255 в фрейм
        public static byte[] Pack(byte[] input, char type)
        {
            if ((input.Length > 255) || (input.Length == 0))
                throw new Exception("Pack() exception: input.Length неподходящая длина");
            byte[] output = new byte[input.Length + 4 + ((type != 'I') ? 1 : 0)];
            // Стартовый байт
            output[0] = 255;
            // Тип кадра
            output[1] = Convert.ToByte(type);
            // Длина значащих байтов
            output[2] = Convert.ToByte(input.Length);
            if (type == 'I')
            {
                for (int i = 0; i < input.Length; i++)
                    output[i + 3] = input[i];
            } else
            if (type == 'F')
            {
                output[3] = Convert.ToByte(pack_amount_write);
            } else
            if (type == 'C')
            {
                output[3] = Convert.ToByte(pack_itr);
            }

            for (int i = 0; i < input.Length; i++)
                output[i + 3 + (type != 'I' ? 1 : 0)] = input[i];
            // Стоповый байт
            output[input.Length + 3 + (type != 'I' ? 1 : 0)] = 255;
            return output;
        }

        public static byte[] Pack(char type)
        {
            byte[] output = new byte[0];

            if ((type == 'L') || (type == 'U') || (type == 'A') || (type == 'R'))
            {
                output = new byte[3];
                output[1] = Convert.ToByte(type);
            }
            else
                throw new Exception("Pack exception: неправильный type");
            // Стопопвый и стартовый биты
            output[0] = 255;
            output[2] = 255;
            return output;
        }

        public static byte[] Unpack(byte[] input)
        {
            byte[] output = new byte[0];
            if ((input[0] != 255) || (input[input.Length - 1] != 255))
                throw new Exception("Unpack() exception: стартовый или стоповый бит отсутствуют");

            char type = Encoding.UTF8.GetChars(new byte[] { input[1] })[0];

            if (type == 'I')
            {
                output = new byte[input[2]];
                for (int i = 0; i < input[2]; i++)
                    output[i] = input[i + 3];
            }
            else if ((type == 'F') || (type == 'C'))
            {
                output = new byte[input[2]];
                for (int i = 0; i < input[2]; i++)
                    output[i] = input[i + 4];
            }
            else if ((type != 'L') && (type != 'R') && (type != 'A') && (type != 'U'))
                throw new Exception("Unpack() exception: Тип кадра не относиться ни к одному из известных программе типов");

            return output;
        }
        
        public void SendLink()
        {
            try
            {
                serial_port.RtsEnable = false;
                var data_sent = Encode(Pack('L'));
                serial_port.Write(data_sent, 0, data_sent.Length);
                serial_port.RtsEnable = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        public void SendUplink()
        {
            try
            {
                serial_port.RtsEnable = false;
                var data_sent = Encode(Pack('U'));
                serial_port.Write(data_sent, 0, data_sent.Length);
                serial_port.RtsEnable = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        public void SendACK()
        {
            try
            {
                serial_port.RtsEnable = false;
                var data_sent = Encode(Pack('A'));
                serial_port.Write(data_sent, 0, data_sent.Length);
                serial_port.RtsEnable = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        public void SendRet()
        {
            try
            {
                serial_port.RtsEnable = false;
                var data_sent = Encode(Pack('R'));
                serial_port.Write(data_sent, 0, data_sent.Length);
                serial_port.RtsEnable = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void textBox2_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                button3.Focus();
                button3_Click(null, null);
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            button1.Enabled = true;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            string basepath = AppDomain.CurrentDomain.BaseDirectory;
            string newpath = basepath + "log\\";
            
            if (!Directory.Exists(newpath))
            {
                //Путь пока не создан... 
                try
                {
                    //Пытаемся создать папку:
                    Directory.CreateDirectory(newpath);
                }
                catch (IOException ex)
                {
                    //В случае ошибок ввода-вывода выдаем сообщение об ошибке
                    MessageBox.Show(ex.Message);
                    //Вновь генерируем обшибку. В случае необходимости реакцию на ошибку ввода-вывода
                    //можно изменить именно тут:
                    throw ex;
                }
                catch (UnauthorizedAccessException ex)
                {
                    //В случае ошибки с нехваткой прав вновь выдаем сообщение:
                    MessageBox.Show(ex.Message);
                    //И вновь генерируем ошибку. Если нужно обработаь ошибку более детально, то тут как раз
                    //самое место это сделать. 
                    throw ex;
                }
            }

            DateTime dt = DateTime.Now;// Дата
            dt = dt.ToUniversalTime();
            String dtn = dt.ToString() +".txt"; // Дата
            dtn = dtn.Replace(":",".");
            dtn = dtn.Replace("*", ".");
            dtn = dtn.Replace("/", ".");
            dtn = dtn.Replace("<", ".");
            dtn = dtn.Replace(">", ".");
            dtn = dtn.Replace("|", ".");
            dtn = dtn.Replace("?", ".");
            SaveFileDialog saveFileDialog2 = new SaveFileDialog();
            saveFileDialog2.FileName = newpath + dtn;
            saveFileDialog2.Filter = "Текстовый файл (.txt)|*.txt|текст в формате Unicode (.txt)|*.txt|Rich Text Format (.rtf)|*.rtf|Все файлы (*.*)|*.*";
            saveFileDialog2.FilterIndex = 2;
            File.WriteAllText(saveFileDialog2.FileName, contents: textBox1.Text);

            if ((serial_port != null) && serial_port.IsOpen)
            {
                if ((link_timer != null) && link_timer.Enabled)
                    link_timer.Stop();

                SendUplink(); // Отправить кадр о закрытии соединения
                serial_port.Close(); // Закрыть порт
            }
        }
    }
}