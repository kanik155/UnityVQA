using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;


public class ChatGPTController : MonoBehaviour
{
    [Serializable]
    public class MessageModel
    {
        public string role;
        public List<Content> content;

        [Serializable]
        public class Content
        {
            public string type;
            public string text;
            public string image_url;
        }
    }

    [Serializable]
    public class CompletionRequestModel
    {
        public string model;
        public List<MessageModel> messages;
        public int max_tokens;
    }

    [Serializable]
    public class ChatGPTRecieveModel
    {
        public string id;
        public string @object;
        public int created;
        public Choice[] choices;
        public Usage usage;

        [Serializable]
        public class Choice
        {
            public int index;
            public ReceiveMessageModel message;
            public string finish_reason;

            [Serializable]
            public class ReceiveMessageModel
            {
                public string role;
                public string content;
            }
        }

        [Serializable]
        public class Usage
        {
            public int prompt_tokens;
            public int completion_tokens;
            public int total_tokens;
        }
    }

    [SerializeField] private string apiKey;
    [SerializeField] Button sendButton;
    [SerializeField] InputField questionInputField;
    private List<MessageModel> messages = new List<MessageModel>();

    private void Start()
    {
        sendButton.onClick.AddListener(OnSendButtonClick);
    }

    public void OnSendButtonClick()
    {
        StartCoroutine(Chat());
    }

    protected IEnumerator Chat()
    {
        yield return new WaitForEndOfFrame();

        var currentScreenShotTexture = new Texture2D(Screen.width, Screen.height);
        currentScreenShotTexture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
        currentScreenShotTexture.Apply();
        var encodedImage = Convert.ToBase64String(currentScreenShotTexture.EncodeToJPG());
        MessageSubmit(questionInputField.text, encodedImage);
    }

    private void Communication(string message, string base64String, Action<MessageModel> result)
    {
        Debug.Log(message);

        List<MessageModel.Content> contentData = new List<MessageModel.Content>
        {
            new MessageModel.Content()
            {
                type = "text",
                text = message
            },
            new MessageModel.Content()
            {
                type = "image_url",
                image_url = "data:image/jpeg;base64," + base64String
            }
        };

        messages.Add(new MessageModel()
        {
            role = "user",
            content = contentData
        });

        var apiUrl = "https://api.openai.com/v1/chat/completions";

        var jsonOptions = JsonUtility.ToJson(
            new CompletionRequestModel()
            {
                model = "gpt-4-vision-preview",
                messages = messages,
                max_tokens = 4096
            }, true);

        var headers = new Dictionary<string, string>
        {
            {"Authorization", "Bearer " + apiKey},
            {"Content-type", "application/json"},
            {"X-Slack-No-Retry", "1"}
        };

        var request = new UnityWebRequest(apiUrl, "POST")
        {
            uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(RemoveEmptyJsonPram(jsonOptions))),
            downloadHandler = new DownloadHandlerBuffer()
        };

        foreach (var header in headers)
        {
            request.SetRequestHeader(header.Key, header.Value);
        }

        var operation = request.SendWebRequest();

        operation.completed += _ =>
        {
            if (operation.webRequest.result == UnityWebRequest.Result.ConnectionError ||
                       operation.webRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError(operation.webRequest.error);
                throw new Exception();
            }
            else
            {
                var responseString = operation.webRequest.downloadHandler.text;
                var responseObject = JsonUtility.FromJson<ChatGPTRecieveModel>(responseString);
                Debug.Log(responseObject.choices[0].message.content);
            }
            request.Dispose();
        };
    }

    public void MessageSubmit(string sendMessage, string base64String)
    {
        Communication(sendMessage, base64String, (result) =>
        {
            Debug.Log(result.content);
        });
    }

    public static string RemoveEmptyJsonPram(string jsonString)
    {
        string s = Regex.Replace(jsonString, "((\\\".*\\\")+(?=\\:))[:]\\s(\\\"\\\"[,\\r\\n])", string.Empty);
        s = Regex.Replace(s, @"^\s+$[\r\n]*", string.Empty, RegexOptions.Multiline);
        s = Regex.Replace(s, @"[,]+(?=\W*\})", string.Empty, RegexOptions.Multiline);
        return s;
    }
}