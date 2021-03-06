﻿using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using UnityEngine;
using System;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;

namespace Kayac
{
	public class DebugServer : IDisposable
	{
		public delegate void OnRequest(
			out string outputHtml,
			NameValueCollection queryString,
			Stream bodyDataStream);

		public void RegisterRequestCallback(string path, OnRequest onRequest)
		{
			callbacks.Add(path, onRequest);
		}

		public DebugServer(int port)
		{
			if (!HttpListener.IsSupported)
			{
				return;
			}
			listener = new HttpListener();
			listener.Prefixes.Add("http://*:" + port + "/");
			callbacks = new Dictionary<string, OnRequest>();
			requestContexts = new Queue<HttpListenerContext>();
			listener.Start();
			listener.BeginGetContext(OnRequestArrival, this);
		}

		public void Dispose()
		{
			if (listener != null)
			{
				listener.Stop();
				listener = null;
			}
		}

		public void ManualUpdate()
		{
			while (requestContexts.Count > 0)
			{
				ProcessRequest(requestContexts.Dequeue());
			}
		}

		// non public ----------------
		HttpListener listener;
		Dictionary<string, OnRequest> callbacks;
		Queue<HttpListenerContext> requestContexts;

		// 別のスレッドから呼ばれ得るのでキューに溜めて、ManualUpdateからコールバックを呼ぶ
		// TODO: 別スレでやっていいい処理であればそのまま実行する、という選択肢はあっていい。その方が性能は良い。
		void OnRequestArrival(IAsyncResult asyncResult)
		{
			var context = listener.EndGetContext(asyncResult);
			listener.BeginGetContext(OnRequestArrival, this); // 次の受け取り
			lock (requestContexts)
			{
				requestContexts.Enqueue(context);
			}
		}

		void ProcessRequest(HttpListenerContext context)
		{
			var request = context.Request;
			var response = context.Response;

			var path = request.RawUrl;
			var queryBegin = path.IndexOf('?');
			if (queryBegin >= 0)
			{
				path = path.Remove(queryBegin);
			}
			OnRequest callback = null;

			if (!callbacks.TryGetValue(path, out callback))
			{
				response.StatusCode = 404;
				response.Close();
			}
			else
			{
				string outputHtml = null;
				try // ユーザ処理でコケたら500返す。それ以外は200にしちゃってるがマズければ番号ももらうようにした方がいいのかも。
				{
					callback(out outputHtml, request.QueryString, request.InputStream);
					request.InputStream.Dispose();
					response.StatusCode = 200;
				}
				catch (System.Exception e)
				{
					Debug.LogException(e);
					response.StatusCode = 500;
				}

				if (outputHtml != null)
				{
					var outputData = System.Text.Encoding.UTF8.GetBytes(outputHtml);
					response.Close(outputData, willBlock: false);
				}
				else
				{
					response.Close();
				}
			}
		}
	}
}