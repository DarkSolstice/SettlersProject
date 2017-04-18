using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public static class FileHelper
{
    public const int MAX_MESSAGE_SIZE = 1000;
    public static string SanitizePath(string path)
    {
        path.Replace(@"\", "/");
        return path;
    }

    public static void SendGameData(byte[] data, playerControl player)
    {
        int offset = 0;
        while (offset < data.Length)
        {
            var temp = new byte[MAX_MESSAGE_SIZE];
            for (int j = 0; j < MAX_MESSAGE_SIZE; j++)
            {
                if (j + offset == data.Length) break;
                temp[j] = data[offset + j];
            }
            player.RpcGetGameData(temp, offset);
            offset += MAX_MESSAGE_SIZE;
        }
    }
}

