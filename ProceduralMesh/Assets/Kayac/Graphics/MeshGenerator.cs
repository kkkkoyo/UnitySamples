﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Kayac
{
	public static class MeshGenerator
	{
		// XZ平面上の点列をもらって、一定高さの壁を作る
		public static bool GenerateWall(
			out Vector3[] vertices,
			out Vector3[] normals,
			out Vector2[] uvs,
			out int[] indices,
			IList<Vector2> positions,
			float height,
			float thickness,
			bool looped,
			bool separateFaces)
		{
			vertices = new Vector3[positions.Count * 4];
			normals = new Vector3[vertices.Length];
			uvs = new Vector2[vertices.Length];
			var indexCount = (positions.Count - 1) * 3 * 6;
			if (looped)
			{
				indexCount += 3 * 6;
			}
			indices = new int[indexCount];
			int vi = 0;
			float halfThickness = 0.5f * thickness;
			for (int i = 0; i < positions.Count; i++)
			{
				Vector2 prev;
				Vector2 next;
				if (i > 0)
				{
					prev = positions[i - 1];
				}
				else if (looped)
				{
					prev = positions[positions.Count - 1];
				}
				else
				{
					prev = positions[0];
				}
				if (i < (positions.Count - 1))
				{
					next = positions[i + 1];
				}
				else if (looped)
				{
					next = positions[0];
				}
				else
				{
					next = positions[positions.Count - 1];
				}

				Vector2 tangent = next - prev;
				tangent.Normalize();
				Vector2 normal; // tangentをXZ平面で90度回したもの
				normal.x = -tangent.y;
				normal.y = tangent.x;
				Vector2 inner = positions[i] - (normal * halfThickness);
				Vector2 outer = positions[i] + (normal * halfThickness);
				// 接地頂点
				float u = (float)i / (float)(positions.Count - 1);
				vertices[vi] = new Vector3(inner.x, 0f, inner.y);
				normals[vi] = new Vector3(-normal.x, 0f, -normal.y);
				uvs[vi] = new Vector2(u, 0f);
				vi++;
				vertices[vi] = new Vector3(inner.x, height, inner.y);
				normals[vi] = new Vector3(-normal.x, 0f, -normal.y);
				uvs[vi] = new Vector2(u, 1f / 3f);
				vi++;
				vertices[vi] = new Vector3(outer.x, height, outer.y);
				normals[vi] = new Vector3(normal.x, 0f, normal.y);
				uvs[vi] = new Vector2(u, 2f / 3f);
				vi++;
				vertices[vi] = new Vector3(outer.x, 0f, outer.y);
				normals[vi] = new Vector3(normal.x, 0f, normal.y);
				uvs[vi] = new Vector2(u, 1f);
				vi++;
			}
			FillStripIndices(indices, 0, 0, positions.Count - 1, 3, looped);

			if (separateFaces)
			{
				SeparateFaces(out vertices, out normals, out uvs, out indices, vertices, uvs, indices);
			}
			return true;
		}

		public static bool GenerateWall(
			Mesh mesh,
			IList<Vector2> positions,
			float height,
			float thickness,
			bool looped,
			bool separateFaces)
		{
			Vector3[] vertices;
			Vector3[] normals;
			Vector2[] uvs;
			int[] indices;
			var ret = false;
			if (GenerateWall(out vertices, out normals, out uvs, out indices, positions, height, thickness, looped, separateFaces))
			{
				FillMesh(mesh, vertices, normals, uvs, indices);
				ret = true;
			}
			return ret;
		}

		public static bool GenerateSphere(
			out Vector3[] verticesOut,
			out Vector3[] normalsOut,
			out int[] indicesOut,
			int subdivision,
			bool separateFaces)
		{
			if (subdivision > 6)
			{
				verticesOut = normalsOut = null;
				indicesOut = null;
				return false;
			}
			GenerateOctahedron(out verticesOut, out normalsOut, out indicesOut, separateFaces: false);
			var table = new VertexEdgeFaceTable(verticesOut, normalsOut, null, indicesOut);
			for (int i = 0; i < subdivision; i++)
			{
				table.SubDivide();
			}
			// 全頂点を球面に移動
			var vertices = table.Vertices;
			for (int i = 0; i < vertices.Count; i++)
			{
				var v = vertices[i];
				v.position.Normalize();
				v.position *= 0.5f; // 半径は0.5に
				v.normal = v.position;
			}
			table.GetArrays(out verticesOut, out normalsOut, out indicesOut);
			if (separateFaces)
			{
				SeparateFaces(out verticesOut, out normalsOut, out indicesOut, verticesOut, indicesOut);
			}
			return true;
		}

		public static bool GenerateSphere(
			Mesh mesh,
			int subdivision,
			bool separateFaces)
		{
			Vector3[] vertices;
			Vector3[] normals;
			int[] indices;
			var ret = false;
			if (GenerateSphere(out vertices, out normals, out indices, subdivision, separateFaces))
			{
				FillMesh(mesh, vertices, normals, null, indices);
				ret = true;
			}
			return ret;
		}

		public static bool GenerateCylinderSide(
			Mesh mesh,
			float height,
			float radius,
			int subdivision,
			bool separateFaces)
		{
			Vector3[] vertices;
			Vector3[] normals;
			Vector2[] uvs;
			int[] indices;
			var ret = false;
			if (GenerateCylinderSide(out vertices, out normals, out uvs, out indices, height, radius, subdivision, separateFaces))
			{
				FillMesh(mesh, vertices, normals, uvs, indices);
				ret = true;
			}
			return ret;
		}

		// 円柱。divは面分割数。最低2だが、2だとただの面になる。
		public static bool GenerateCylinderSide(
			out Vector3[] vertices,
			out Vector3[] normals,
			out Vector2[] uvs,
			out int[] indices,
			float height,
			float radius,
			int subdivision,
			bool separateFaces)
		{
			int div = 4 << subdivision;
			if ((div < 2) || (div >= 0x8000))
			{
				vertices = normals = null;
				indices = null;
				uvs = null;
				return false;
			}
			vertices = new Vector3[div * 2];
			normals = new Vector3[vertices.Length];
			uvs = new Vector2[vertices.Length];
			var indexCount = div * 6;
			indices = new int[indexCount];
			var hHeight = height * 0.5f;
			int vi = 0;
			for (int i = 0; i < div; i++)
			{
				var t = (float)i / (float)div;
				var angle = 2f * Mathf.PI * t;
				var x = Mathf.Cos(angle) * radius;
				var z = Mathf.Sin(angle) * radius;
				vertices[vi] = new Vector3(x, -hHeight, z);
				normals[vi] = new Vector3(x, 0f, z);
				uvs[vi] = new Vector2(t, 0f);
				vi++;
				vertices[vi] = new Vector3(x, hHeight, z);
				normals[vi] = new Vector3(x, 0f, z);
				uvs[vi] = new Vector2(t, 1f);
				vi++;
			}
			FillStripIndices(indices, 0, 0, div - 1, 1, true);
			if (separateFaces)
			{
				SeparateFaces(out vertices, out normals, out uvs, out indices, vertices, uvs, indices);
			}
			return true;
		}

		// 角で法線共有しているために滑らか。基本再分割してより滑らかな図形を作るための種として使う。
		public static void GenerateCube(
			out Vector3[] vertices,
			out Vector3[] normals,
			out int[] indices,
			bool separateFaces)
		{
			vertices = new Vector3[8];
			indices = new int[36];
			vertices[0] = new Vector3(-0.5f, -0.5f, -0.5f);
			vertices[1] = new Vector3(-0.5f, -0.5f, 0.5f);
			vertices[2] = new Vector3(-0.5f, 0.5f, -0.5f);
			vertices[3] = new Vector3(-0.5f, 0.5f, 0.5f);
			vertices[4] = new Vector3(0.5f, -0.5f, -0.5f);
			vertices[5] = new Vector3(0.5f, -0.5f, 0.5f);
			vertices[6] = new Vector3(0.5f, 0.5f, -0.5f);
			vertices[7] = new Vector3(0.5f, 0.5f, 0.5f);
			normals = vertices;
			var start = 0;
			start = SetQuad(indices, start, 0, 1, 3, 2);
			start = SetQuad(indices, start, 4, 5, 7, 6);
			start = SetQuad(indices, start, 0, 4, 6, 2);
			start = SetQuad(indices, start, 1, 5, 7, 3);
			start = SetQuad(indices, start, 0, 1, 5, 4);
			start = SetQuad(indices, start, 2, 3, 7, 6);
			if (separateFaces)
			{
				SeparateFaces(out vertices, out normals, out indices, vertices, indices);
			}
		}

		// 角で法線共有しているために滑らか。基本再分割してより滑らかな図形を作るための種として使う。
		public static void GenerateTetrahedron(
			out Vector3[] vertices,
			out Vector3[] normals,
			out int[] indices,
			bool separateFaces)
		{
			vertices = new Vector3[4];
			indices = new int[12];
			vertices[0] = new Vector3(0f, 1f, 0f);
			vertices[1] = new Vector3(Mathf.Sqrt(8f / 9f), -1f / 3f, 0f);
			vertices[2] = new Vector3(-Mathf.Sqrt(2f / 9f), -1f / 3f, Mathf.Sqrt(2f / 3f));
			vertices[3] = new Vector3(-Mathf.Sqrt(2f / 9f), -1f / 3f, -Mathf.Sqrt(2f / 3f));
			normals = vertices;
			var start = 0;
			start = SetTriangle(indices, start, 0, 2, 1);
			start = SetTriangle(indices, start, 0, 3, 2);
			start = SetTriangle(indices, start, 0, 1, 3);
			start = SetTriangle(indices, start, 1, 2, 3);
			if (separateFaces)
			{
				SeparateFaces(out vertices, out normals, out indices, vertices, indices);
			}
		}

		// 角で法線共有しているために滑らか。基本再分割してより滑らかな図形を作るための種として使う。
		public static void GenerateOctahedron(
			out Vector3[] vertices,
			out Vector3[] normals,
			out int[] indices,
			bool separateFaces)
		{
			vertices = new Vector3[6];
			indices = new int[24];
			vertices[0] = new Vector3(-0.5f, 0f, 0f);
			vertices[1] = new Vector3(0f, -0.5f, 0f);
			vertices[2] = new Vector3(0f, 0f, -0.5f);
			vertices[3] = new Vector3(0f, 0f, 0.5f);
			vertices[4] = new Vector3(0f, 0.5f, 0f);
			vertices[5] = new Vector3(0.5f, 0f, 0f);
			normals = vertices;
			var start = 0;
			start = SetTriangle(indices, start, 0, 1, 2);
			start = SetTriangle(indices, start, 0, 2, 4);
			start = SetTriangle(indices, start, 3, 1, 0);
			start = SetTriangle(indices, start, 3, 0, 4);
			start = SetTriangle(indices, start, 5, 1, 3);
			start = SetTriangle(indices, start, 5, 3, 4);
			start = SetTriangle(indices, start, 2, 1, 5);
			start = SetTriangle(indices, start, 2, 5, 4);
			if (separateFaces)
			{
				SeparateFaces(out vertices, out normals, out indices, vertices, indices);
			}
		}

		// 全ての共有頂点を複製し、法線の再計算を行う
		public static void SeparateFaces(
			out Vector3[] verticesOut,
			out Vector3[] normalsOut,
			out Vector2[] uvsOut,
			out int[] indicesOut,
			IList<Vector3> verticesIn,
			IList<Vector2> uvsIn,
			IList<int> indicesIn)
		{
			Debug.Assert((indicesIn.Count % 3) == 0);
			verticesOut = new Vector3[indicesIn.Count];
			normalsOut = new Vector3[indicesIn.Count];
			uvsOut = null;
			if (uvsIn != null)
			{
				uvsOut = new Vector2[indicesIn.Count];
			}
			indicesOut = new int[indicesIn.Count];

			// モリモリ複製しつつ法線は再計算する
			for (int i = 0; i < indicesIn.Count; i += 3)
			{
				var i0 = indicesIn[i + 0];
				var i1 = indicesIn[i + 1];
				var i2 = indicesIn[i + 2];
				var v0 = verticesIn[i0];
				var v1 = verticesIn[i1];
				var v2 = verticesIn[i2];
				verticesOut[i + 0] = verticesIn[i0];
				verticesOut[i + 1] = verticesIn[i1];
				verticesOut[i + 2] = verticesIn[i2];
				if (uvsIn != null)
				{
					uvsOut[i + 0] = uvsIn[i0];
					uvsOut[i + 1] = uvsIn[i1];
					uvsOut[i + 2] = uvsIn[i2];
				}
				var v10 = v1 - v0;
				var v20 = v2 - v0;
				var n = Vector3.Cross(v10, v20);
				n.Normalize();
				normalsOut[i + 0] = normalsOut[i + 1] = normalsOut[i + 2] = n;
				// インデクスは単純
				indicesOut[i + 0] = i + 0;
				indicesOut[i + 1] = i + 1;
				indicesOut[i + 2] = i + 2;
			}
		}

		public static void SeparateFaces(
			out Vector3[] verticesOut,
			out Vector3[] normalsOut,
			out int[] indicesOut,
			IList<Vector3> verticesIn,
			IList<int> indicesIn)
		{
			// UV作っちゃって捨てる
			Vector2[] uvsOutUnused = null;
			SeparateFaces(
				out verticesOut,
				out normalsOut,
				out uvsOutUnused,
				out indicesOut,
				verticesIn,
				null,
				indicesIn);
		}


		// non-public ------------------
		static void FillStripIndices(int[] indices, int indexStart, int vertexStart, int stripLength, int stripWidth, bool looped)
		{
			var start = 0;
			int vi = vertexStart;
			int vUnit = stripWidth + 1;
			for (int li = 0; li < stripLength; li++)
			{
				for (int wi = 0; wi < stripWidth; wi++)
				{
					start = SetQuad(
						indices,
						start,
						vi + wi,
						vi + wi + 1,
						vi + vUnit + wi + 1,
						vi + vUnit + wi);
				}
				vi += vUnit;
			}
			if (looped)
			{
				for (int wi = 0; wi < stripWidth; wi++)
				{
					start = SetQuad(
						indices,
						start,
						vi + wi,
						vi + wi + 1,
						wi + 1,
						wi);
				}
			}
		}

		static void FillMesh(
			Mesh mesh,
			Vector3[] vertices,
			Vector3[] normals,
			Vector2[] uvs,
			int[] indices)
		{
			mesh.Clear();
			mesh.vertices = vertices;
			mesh.normals = normals;
			if (uvs != null)
			{
				mesh.uv = uvs;
			}
			mesh.SetIndices(indices, MeshTopology.Triangles, 0);
		}

		static int SetTriangle(int[] indices, int start, int v0, int v1, int v2)
		{
			indices[start + 0] = v0;
			indices[start + 1] = v1;
			indices[start + 2] = v2;
			return start + 3;
		}

		static int SetQuad(int[] indices, int start, int v0, int v1, int v2, int v3)
		{
			indices[start + 0] = v0;
			indices[start + 1] = v1;
			indices[start + 2] = v2;
			indices[start + 3] = v2;
			indices[start + 4] = v3;
			indices[start + 5] = v0;
			return start + 6;
		}
	}

	public class VertexEdgeFaceTable
	{
		public VertexEdgeFaceTable(
			IList<Vector3> positions,
			IList<Vector3> normals,
			IList<Vector2> uvs,
			IList<int> indices)
		{
			vertices = new List<Vertex>();
			edges = new List<Edge>();
			faces = new List<Face>();

			// まず頂点充填
			Debug.Assert(positions.Count == normals.Count);
			if (uvs != null)
			{
				Debug.Assert(positions.Count == uvs.Count);
				for (int i = 0; i < positions.Count; i++)
				{
					vertices.Add(new Vertex(positions[i], normals[i], uvs[i]));
				}
			}
			else
			{
				for (int i = 0; i < positions.Count; i++)
				{
					vertices.Add(new Vertex(positions[i], normals[i], Vector2.zero));
				}
			}

			// 次に辺を充填。HashSetで重複を回避する。
			var edgeSet = new HashSet<uint>();
			for (int i = 0; i < indices.Count; i += 3)
			{
				var vi0 = indices[i + 0];
				var vi1 = indices[i + 1];
				var vi2 = indices[i + 2];
				var e01 = EdgeKey(vi0, vi1);
				var e12 = EdgeKey(vi1, vi2);
				var e20 = EdgeKey(vi2, vi0);
				if (!edgeSet.Contains(e01))
				{
					edgeSet.Add(e01);
				}
				if (!edgeSet.Contains(e12))
				{
					edgeSet.Add(e12);
				}
				if (!edgeSet.Contains(e20))
				{
					edgeSet.Add(e20);
				}
			}

			// 辺セットを配列に充填しつつ、頂点インデクス→辺インデクスの辞書を用意
			var edgeMap = new Dictionary<uint, int>();
			foreach (var edgeKey in edgeSet)
			{
				var vi0 = (int)(edgeKey >> 16);
				var vi1 = (int)(edgeKey & 0xffff);
				edgeMap.Add(edgeKey, edges.Count);
				edges.Add(new Edge(vi0, vi1));
			}

			// 面を充填開始
			for (int i = 0; i < indices.Count; i += 3)
			{
				var vi0 = indices[i + 0];
				var vi1 = indices[i + 1];
				var vi2 = indices[i + 2];
				var e01 = EdgeKey(vi0, vi1);
				var e12 = EdgeKey(vi1, vi2);
				var e20 = EdgeKey(vi2, vi0);
				var ei01 = edgeMap[e01];
				var ei12 = edgeMap[e12];
				var ei20 = edgeMap[e20];
				faces.Add(new Face(ei01, ei12, ei20));
			}
		}

		public IList<Vertex> Vertices { get { return vertices; } }

		public override string ToString()
		{
			var sb = new System.Text.StringBuilder();
			sb.AppendLine("[Vertices]");
			for (int i = 0; i < vertices.Count; i++)
			{
				var v = vertices[i];
				sb.AppendFormat("\t{0}: {1} {1} {2}\n", i, v.position, v.normal, v.uv);
			}
			sb.AppendLine("[Edges]");
			for (int i = 0; i < edges.Count; i++)
			{
				sb.AppendFormat("\t{0}: {1} {2}\n", i, edges[i].v0, edges[i].v1);
			}
			sb.AppendLine("[Faces]");
			for (int i = 0; i < faces.Count; i++)
			{
				sb.AppendFormat("\t{0}: {1} {2} {3}\n", i, faces[i].e0, faces[i].e1, faces[i].e2);
			}
			return sb.ToString();
		}

		// 全ての辺を二等分して頂点を生成し、分割面を生成する。面の数は4倍になる。
		public void SubDivide()
		{
			var oldVn = vertices.Count;
			var oldEn = edges.Count;
			var oldFn = faces.Count;
			// 全edgeの中点を生成して追加
			for (int i = 0; i < oldEn; i++)
			{
				var edge = edges[i];
				var v0 = vertices[edge.v0];
				var v1 = vertices[edge.v1];
				var midPoint = (v0.position + v1.position) * 0.5f;
				var midNormal = (v0.normal + v1.normal).normalized;
				var midUv = (v0.uv + v1.uv) * 0.5f;
				vertices.Add(new Vertex(midPoint, midNormal, midUv));
			}

			// faceの分割。edgeが古いうちにやる
			for (int i = 0; i < oldFn; i++)
			{
				var face = faces[i];
				var e0 = edges[face.e0];
				var e1 = edges[face.e1];
				var e2 = edges[face.e2];
				// 4分割する
				// 関連する辺は3 -> 9
				// e0の中点とe1の中点からなる新エッジ = oldEn + (i * 3) + 0
				// e1の中点とe2の中点からなる新エッジ = oldEn + (i * 3) + 1
				// e2の中点とe0の中点からなる新エッジ = oldEn + (i * 3) + 2
				// e0は、e0とoldEn + (oldFn * 3) + e0 に分割
				// e1は、e1とoldEn + (oldFn * 3) + e1 に分割
				// e2は、e2とoldEn + (oldFn * 3) + e2 に分割

				var newFace0 = MakeDividedFace(i, 0, oldEn, oldFn, face.e0, face.e1);
				var newFace1 = MakeDividedFace(i, 1, oldEn, oldFn, face.e1, face.e2);
				var newFace2 = MakeDividedFace(i, 2, oldEn, oldFn, face.e2, face.e0);
				faces.Add(newFace0);
				faces.Add(newFace1);
				faces.Add(newFace2);
				// 辺を生成
				var newEdge0 = new Edge(oldVn + face.e0, oldVn + face.e1);
				var newEdge1 = new Edge(oldVn + face.e1, oldVn + face.e2);
				var newEdge2 = new Edge(oldVn + face.e2, oldVn + face.e0);
				edges.Add(newEdge0);
				edges.Add(newEdge1);
				edges.Add(newEdge2);
				// 自分は中点3点からなる面に変換
				face.e0 = oldEn + (i * 3) + 0;
				face.e1 = oldEn + (i * 3) + 1;
				face.e2 = oldEn + (i * 3) + 2;
			}

			// edgeの分割
			for (int i = 0; i < oldEn; i++)
			{
				var edge = edges[i];
				// 新しい頂点を使ってedgeを分割
				var midVi = oldVn + i;
				var newEdge = new Edge(midVi, edge.v1);
				edges.Add(newEdge);
				// 自分は終点を新しい点に変更
				edge.v1 = midVi;
			}
		}

		public void GetArrays(
			out Vector3[] verticesOut,
			out Vector3[] normalsOut,
			out Vector2[] uvsOut,
			out int[] indicesOut)
		{
			verticesOut = new Vector3[vertices.Count];
			normalsOut = new Vector3[vertices.Count];
			uvsOut = new Vector2[vertices.Count];
			for (int i = 0; i < vertices.Count; i++)
			{
				verticesOut[i] = vertices[i].position;
				normalsOut[i] = vertices[i].normal;
				uvsOut[i] = vertices[i].uv;
			}
			indicesOut = MakeIndices();
		}

		public void GetArrays(
			out Vector3[] verticesOut,
			out Vector3[] normalsOut,
			out int[] indicesOut)
		{
			Vector2[] uvsUnused;
			GetArrays(out verticesOut, out normalsOut, out uvsUnused, out indicesOut);
		}

		public int[] MakeIndices()
		{
			var ret = new int[faces.Count * 3]; // 数は決まっている
			for (int i = 0; i < faces.Count; i++) // 各面についてインデクスを生成
			{
				int vi0, vi1, vi2;
				MakeIndices(out vi0, out vi1, out vi2, i);
				// 法線とのチェック
				var v0 = vertices[vi0].position;
				var v1 = vertices[vi1].position;
				var v2 = vertices[vi2].position;
				var cross = Vector3.Cross(v1 - v0, v2 - v0);
				var dp = Vector3.Dot(cross, vertices[vi0].normal);
				if (dp < 0f) // 法線が合わないので反転
				{
					var tmp = vi1;
					vi1 = vi2;
					vi2 = tmp;
				}
				ret[(i * 3) + 0] = vi0;
				ret[(i * 3) + 1] = vi1;
				ret[(i * 3) + 2] = vi2;
			}
			return ret;
		}

		void MakeIndices(out int vi0, out int vi1, out int vi2, int fi)
		{
			var f = faces[fi];
			var e0 = edges[f.e0];
			var e1 = edges[f.e1];
			var e2 = edges[f.e2];
			vi0 = e0.v0;
			vi1 = e0.v1;
			if ((e1.v0 != vi0) && (e1.v0 != vi1))
			{
				vi2 = e1.v0;
			}
			else
			{
				vi2 = e1.v1;
			}
			// e2の頂点は両方すでに見つかっているはず
			Debug.Assert((e2.v0 == vi0) || (e2.v0 == vi1) || (e2.v0 == vi2));
			Debug.Assert((e2.v1 == vi0) || (e2.v1 == vi1) || (e2.v1 == vi2));
		}

		// non-public -----------------------
		class Edge
		{
			public Edge(int v0, int v1)
			{
				this.v0 = v0;
				this.v1 = v1;
			}
			public int v0, v1;
		}

		class Face
		{
			public Face(int e0, int e1, int e2)
			{
				this.e0 = e0;
				this.e1 = e1;
				this.e2 = e2;
			}
			public int e0, e1, e2;
		}

		public class Vertex
		{
			public Vertex(Vector3 position, Vector3 normal, Vector3 uv)
			{
				this.position = position;
				this.normal = normal;
				this.uv = uv;
			}
			public Vector3 position;
			public Vector3 normal;
			public Vector2 uv;
		}

		Face MakeDividedFace(int fi, int divIndex, int oldEn, int oldFn, int oldEi0, int oldEi1)
		{
			// 新しく面を生成
			int ei0 = oldEn + (fi * 3) + divIndex;
			int ei1, ei2;
			Edge e0 = edges[oldEi0];
			Edge e1 = edges[oldEi1];
			if ((e0.v0 == e1.v0) || (e0.v0 == e1.v1)) // e0.v0が頂点
			{
				ei1 = oldEi0;
				if (e0.v0 == e1.v0)
				{
					ei2 = oldEi1;
				}
				else
				{
					ei2 = oldEn + (oldFn * 3) + oldEi1;
				}
			}
			else if ((e0.v1 == e1.v0) || (e0.v1 == e1.v1)) // e0.v1が頂点
			{
				ei1 = oldEn + (oldFn * 3) + oldEi0;
				if (e0.v1 == e1.v0)
				{
					ei2 = oldEi1;
				}
				else
				{
					ei2 = oldEn + (oldFn * 3) + oldEi1;
				}
			}
			else // バグ
			{
				Debug.Assert(false);
				ei1 = ei2 = int.MaxValue; // 死ぬべき
			}
			var newFace = new Face(ei0, ei1, ei2);
			return newFace;
		}

		uint EdgeKey(int vi0, int vi1)
		{
			Debug.Assert(vi0 <= 0xffff);
			Debug.Assert(vi1 <= 0xffff);
			if (vi0 > vi1)
			{
				var tmp = vi0;
				vi0 = vi1;
				vi1 = tmp;
			}
			return (uint)((vi0 << 16) | vi1);
		}

		List<Vertex> vertices;
		List<Edge> edges;
		List<Face> faces;
	}
}