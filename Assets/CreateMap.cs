using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

public class Pos
{
	public int x;
	public int y;

	public Pos() {
	}

	public Pos(Pos p)
	{
		x = p.x;
		y = p.y;
	}
	public Pos(int x, int y)
	{
		this.x = x;
		this.y = y;
	}
	public static float AStarDistance(Pos p1, Pos p2)//F = G + H这里为H当前点到终点的代价
	{
		float d1 = Mathf.Abs(p1.x - p2.x);
		float d2 = Mathf.Abs(p1.y - p2.y);
		return d1 + d2;
	}
	// 定义了Equals函数，判断相等时比较方便。p.Equals(q)，就可以判断p和q是否相等了。
	public bool Equals(Pos p)
	{
		return x == p.x && y == p.y;
	}
}

public class AScore
{
	// G是从起点出发的步数
	public float G = 0;
	// H是估算的离终点距离
	public float H = 0;

	public bool closed = false;
	public Pos parent = null;

	public AScore(float g, float h)
	{
		G = g;
		H = h;
		closed = false;
	}
	//Fcost
	public float F
	{
		get { return G + H; }
	}

	public int CompareTo(AScore a2)
	{
		if (F == a2.F)
		{
			return 0;
		}
		if (F > a2.F)
		{
			return 1;
		}
		return -1;
	}


	public bool Equals(AScore a)
	{
		if (a.F==F)
		{
			return true;
		}

		return false;
	}
}

public class CreateMap : MonoBehaviour {
	int W = 30;//Column
	int H = 20;//Row

	private int[,] map;
	public GameObject prefab_wall;
	public GameObject prefab_start;
	public GameObject prefab_end;
	public GameObject prefab_path;
	public GameObject prefab_way;
	
	GameObject pathParent;
	
	const int START = 8;
	const int END = 9;
	const int WALL = 1;
	
	Pos startPos;
	Pos endPos;
	
	public enum SearchWay
	{
		BFS,
		DFS,
		AStar,
	}
	public SearchWay searchWay = SearchWay.BFS;
	
	enum GameState
	{
		SetBeginPoint,
		SetEndPoint,
		StartCalculation,
		Calculation,
		ShowPath,
		Finish,
	}
	GameState gameState = GameState.SetBeginPoint;
	
	void InitMap0()
	{
		var walls = new GameObject();
		walls.name = "walls";
		for (int i = 0; i < H; i++)
		{
			for (int j = 0; j < W; j++)
			{
				if (map[i,j]==WALL)
				{
					var go = Instantiate(prefab_wall, new Vector3(j * 1, 0.5f, i * 1), Quaternion.identity,
						walls.transform);
				}
			}
		}
	}

	public void ReadMapFile()
	{
		
		string path = Application.dataPath + "//" + "map.txt";
		if (!File.Exists(path))
		{
			return;
		}
		FileStream fs=new FileStream(path,FileMode.Open,FileAccess.Read);
		StreamReader read=new StreamReader(fs,Encoding.Default);
		string strReadLine = "";
		int y = 0;
		read.ReadLine();
		strReadLine = read.ReadLine();
		while (strReadLine!=null&&y<H)
		{
			int t;
			for (int x = 0;x<W&& x < strReadLine.Length; ++x)
			{
				switch(strReadLine[x])
				{
					case '1':
						t = 1;
						break;
					case '8':
						t = 8;
						break;
					case '9':

						t = 9;
						break;
					default:
						t = 0;
						break;
				}

				map[y, x] = t;
			}

			y += 1;
			strReadLine = read.ReadLine();
		}
		read.Dispose();
		fs.Close();
	}
	void Start ()
	{
		pathParent = GameObject.Find("PathParent");
		map=new int[H,W];
		ReadMapFile();
		InitMap0();
	}

	bool SetPoint(int n)
	{
		if (Input.GetMouseButtonDown(0))
		{
			Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
			RaycastHit hitt=new RaycastHit();
			Physics.Raycast(ray, out hitt);
			if (hitt.transform != null && hitt.transform.name == "Ground")
			{
				int x = (int)hitt.point.x;
				int y = (int)hitt.point.z;

				map[y, x] = n;//真实地图被start和end标记
				if (n==START)
				{
					startPos=new Pos(x,y);//创建标记坐标
				}else if (n==END)
				{
					endPos=new Pos(x,y);
				}

				return true;
			}
		}
		return false;
	}
	void Update () {
		switch (gameState)
		{
				case GameState.SetBeginPoint:
					if (SetPoint(START))
					{
						Refresh();
						gameState = GameState.SetEndPoint;
					}
					break;
				case GameState.SetEndPoint:
					if (SetPoint(END))
					{
						Refresh();
						gameState = GameState.StartCalculation;
					}
					break;
				case GameState.StartCalculation:
					if (searchWay==SearchWay.BFS)
					{
						StartCoroutine(BFS());
					}

					if (searchWay==SearchWay.DFS)
					{
						StartCoroutine(DFS());
					}

					if (searchWay==SearchWay.AStar)
					{
						StartCoroutine(AStar());
					}
					gameState = GameState.Calculation;
					break;
				case GameState.Calculation:
					
					break;
				case GameState.ShowPath:
					if (searchWay==SearchWay.BFS)
					{
						StartCoroutine(BFSShowPath());
						gameState = GameState.Finish;
					}else 
					if (searchWay==SearchWay.DFS)
					{
						StartCoroutine(BFSShowPath());
						gameState = GameState.Finish;
					}else 
					if (searchWay==SearchWay.AStar)
					{
						StartCoroutine(AStarShowPath());
						gameState = GameState.Finish;
					}
					break;
				case GameState.Finish:
					break;
		}
	}

	delegate bool Func(Pos cur, int ox, int oy);
	#region AStar

	private AScore[,] astar_search;//是一个而为的点的信息，点里包含权重，F = G + H ; G为点要走过自身的格子距离，H为到目标点的距离
	IEnumerator AStar()
	{
		astar_search =new AScore[map.GetLength(0),map.GetLength(1)];//确保星可以遍布在地图每个点
		List<Pos>MapTargetPoslist=new List<Pos>();//只用来保存要涉及到的点的坐标
		
		astar_search[startPos.y,startPos.x]=new AScore(0,0);//让第一个起始点的G和H为0
		MapTargetPoslist.Add(startPos);//将起始点记录进我们路线 ，因为是从此出发

		Func func = (Pos curPos, int ox, int oy) =>
		{
			var tar_Score = astar_search[curPos.y + oy, curPos.x + ox];//表示下一个去的点；
			if (tar_Score!=null&&tar_Score.closed)
			{
				return false;
			}

			var cur_Score = astar_search[curPos.y, curPos.x];//表示当前所在的点
			Pos nextPos=new Pos(curPos.x+ox,curPos.y+oy);
			
			if (map[curPos.y+oy,curPos.x+ox]==END)
			{
				var end_Score = new AScore(cur_Score.G + 1, 0);
				end_Score.parent = curPos;
				astar_search[curPos.y + oy, curPos.x + ox] = end_Score;
				Debug.Log("Done");
				return true;
			}
			if (map[curPos.y+oy,curPos.x+ox]==0)//表示下一个点我们还没走过
			{
				if (tar_Score==null)
				{
					var a=new AScore(cur_Score.G+1,Pos.AStarDistance(nextPos,endPos));//AStarDistance计算了下一个点 中点到endPos的距离放在H里，然后创建下个点
					a.parent = curPos;
					astar_search[curPos.y + oy, curPos.x + ox] = a;
					MapTargetPoslist.Add(nextPos);
					//Debug.Log("next "+curPos.x+" "+curPos.y);
				}
			}
			return false;
		};
		while (MapTargetPoslist.Count>0)
		{
			MapTargetPoslist.Sort((Pos p1, Pos p2) =>
			{
				AScore a1 = astar_search[p1.y, p1.x];
				AScore a2 = astar_search[p2.y, p2.x];
				return a1.CompareTo(a2);
			});//得到F最小的点放在list[0]
			Pos cur = MapTargetPoslist[0];
			MapTargetPoslist.RemoveAt(0);
			astar_search[cur.y, cur.x].closed = true;//标记当前点为 以访问;
			// 上
			if (cur.y > 0)
			{
				if (func(cur, 0, -1)) { break; }
			}
			// 下
			if (cur.y < H - 1)
			{
				if (func(cur, 0, 1)) { break; }
			}
			// 左
			if (cur.x > 0)
			{
				if (func(cur, -1, 0)) { break; }
			}
			// 右
			if (cur.x < W - 1)
			{
				if (func(cur, 1, 0)) { break; }
			}
			
			short[,] temp_map = new short[map.GetLength(0), map.GetLength(1)];
			for (int i=0; i<H; ++i)
			{
				for (int j=0; j<W; ++j)
				{
					temp_map[i, j] = short.MaxValue;
					//if (map_search[i,j] != null && map_search[i,j].closed)
					if (astar_search[i,j] != null)
					{
						temp_map[i, j] = (short)astar_search[i, j].F;
					}
				}
			}
			RefreshPath(temp_map);
			yield return 0;
		}
		Debug.Log("开始显示路线");
		gameState = GameState.ShowPath;
		yield return null;
	}
	
   
    
	#endregion
	#region BFS
	//===============-BFS-================================================================================
	private int cur_depth = 0;
	private short[,] bfs_search = null;//标记地图
	
	IEnumerator BFS()
	{
		bfs_search=new short[map.GetLength(0),map.GetLength(1)];
		// map_search和map一样大小，每个元素的值：32767(short.MaxValue)代表不可通过或者未探索，其他值代表移动的步数
		for (int i = 0; i < H; ++i)
		{
			for (int j = 0; j < W; ++j)
			{
				bfs_search[i, j] = short.MaxValue;//先初始化一个标记数组。
			}
		}
		Queue<Pos> queue=new Queue<Pos>();
		Func func = (Pos cur, int ox, int oy) =>
		{
			//END=9/START=8
			if (map[cur.y+oy,cur.x+ox]==END)//到达END=9
			{
				bfs_search[cur.y + oy, cur.x + ox] = (short)(bfs_search[cur.y, cur.x] + 1);
				Debug.Log("Done"+(int)bfs_search[cur.y + oy, cur.x + ox] );
				return true;
			}
			//起始点的下一个点不为墙并且在地图内
			if (map[cur.y+oy,cur.x+ox]==0)//bfs_search[startpos,startpos]=[0,0]下面设置，这里判断是否在地图内和不是墙
			{
				if (bfs_search[cur.y+oy,cur.x+ox]>bfs_search[cur.y,cur.x]+1)//最初始的bfs_search[cur.y,cur.x]=0；
				{
					bfs_search[cur.y + oy, cur.x + ox] = (short) (bfs_search[cur.y, cur.x] + 1);//搜索过的地方变成1,标记将去的点为已走过
					queue.Enqueue(new Pos(cur.x+ox,cur.y+oy));//第二个点入队列表示为current了
					// 0-0-0 		 0-0-0 	   0-0-0 map
					// 0-short-short 0-1-short 0-1-2 bfs_search
				}
			}
			return false;
		};
		bfs_search[startPos.y, startPos.x] = 0;//设置起始点的步数为0
		queue.Enqueue(startPos);
		while (queue.Count>0)
		{
			Pos cur = queue.Dequeue();
			//上
			if (cur.y>0)
			{
				//000
				//000
				//000  ->2 往上移动为2-1=1 变成第一行
				if (func(cur,0,-1))
				{
					break;
				}
			}
			//下
			if (cur.y<H-1)
			{
				if (func(cur,0,1))
				{
					break;
				}
			}
			//左
			if (cur.x>0)
			{
				if (func(cur,-1,0))
				{
					break;
				}
			}
			//右
			if (cur.x<W-1)
			{
				if (func(cur,1,0))
				{
					break;
				}
			}
			if (bfs_search[cur.y,cur.x]>cur_depth)//最初cur_depth=0 第一次后变成1
			{
				cur_depth = bfs_search[cur.y, cur.x];
				RefreshPath(bfs_search);
				yield return new WaitForSeconds(0.1f);
			}
		}
		gameState = GameState.ShowPath;
		yield  return null;
	}
	//===============-BFS-================================================================================
	#endregion
	#region DFS
	//===============-DFS-================================================================================
	IEnumerator DFS()
	{
		bfs_search=new short[map.GetLength(0),map.GetLength(1)];
		// map_search和map一样大小，每个元素的值：32767(short.MaxValue)代表不可通过或者未探索，其他值代表移动的步数
		for (int i = 0; i < H; ++i)
		{
			for (int j = 0; j < W; ++j)
			{
				bfs_search[i, j] = short.MaxValue;//先初始化一个标记数组。
			}
		}
		List<Pos> queue=new List<Pos>();
		Func func = (Pos cur, int ox, int oy) =>
		{
			//END=9/START=8
			if (map[cur.y+oy,cur.x+ox]==END)//到达END=9
			{
				bfs_search[cur.y + oy, cur.x + ox] = (short)(bfs_search[cur.y, cur.x] + 1);//==-32768
				Debug.Log("Done");
				return true;
			}
			//起始点的下一个点不为墙并且在地图内
			if (map[cur.y+oy,cur.x+ox]==0)//bfs_search[startpos,startpos]=[0,0]下面设置，这里判断是否在地图内和不是墙
			{
				if (bfs_search[cur.y+oy,cur.x+ox]>bfs_search[cur.y,cur.x]+1)//最初始的bfs_search[cur.y,cur.x]=0；
				{
					bfs_search[cur.y + oy, cur.x + ox] = (short) (bfs_search[cur.y, cur.x] + 1);//搜索过的地方变成1,标记将去的点为已走过
					queue.Add(new Pos(cur.x+ox,cur.y+oy));//第二个点入队列表示为current了
				}
			}
			return false;
		};
		bfs_search[startPos.y, startPos.x] = 0;//设置起始点的步数为0
		queue.Add(startPos);
		while (queue.Count>0)
		{
			int min_i = 0;
			Pos cur = queue[min_i];
			float min_dist = Pos.AStarDistance(cur, endPos);
			for (int i = 0; i <queue.Count; i++)
			{
				float d = Pos.AStarDistance(queue[i], endPos);
				if (d<min_dist)
				{
					min_i = i;
					cur = queue[i];
					min_dist = 0;
				}
			}
			queue.RemoveAt(min_i);
			//上
			if (cur.y>0)
			{
				//000
				//000
				//000  ->2 往上移动为2-1=1 变成第一行
				if (func(cur,0,-1))
				{
					break;
				}
			}
			//下
			if (cur.y<H-1)
			{
				if (func(cur,0,1))
				{
					break;
				}
			}
			//左
			if (cur.x>0)
			{
				if (func(cur,-1,0))
				{
					break;
				}
			}
			//右
			if (cur.x<W-1)
			{
				if (func(cur,1,0))
				{
					break;
				}
			}
			if (bfs_search[cur.y,cur.x]>cur_depth)//最初cur_depth=0 第一次后变成1
			{
				cur_depth = bfs_search[cur.y, cur.x];
				RefreshPath(bfs_search);
				yield return new WaitForSeconds(0.1f);
			}
		}
		gameState = GameState.ShowPath;
		yield  return null;
	}
	//===============-BFS-================================================================================
	#endregion

	
	
	
	IEnumerator BFSShowPath()
	{
		Pos p = endPos;
		while (true)
		{
			int currentStep = bfs_search[p.y, p.x];
			if (currentStep==0)
			{
				break;
			}

			if (p.y>0 && bfs_search[p.y-1, p.x] == currentStep - 1)
			{
				p.y -= 1;
			} else if (p.y<bfs_search.GetLength(0)-1 && bfs_search[p.y+1, p.x] == currentStep - 1)
			{
				p.y += 1;
			}
			else if (p.x>0 && bfs_search[p.y, p.x-1] == currentStep - 1)
			{
				p.x -= 1;
			}
			else if (p.x<bfs_search.GetLength(1)-1 && bfs_search[p.y, p.x+1] == currentStep - 1)
			{
				p.x += 1;
			}
			if (!p.Equals(startPos))
			{
				var go = Instantiate(prefab_way, new Vector3(p.x * 1, 0.5f, p.y * 1), Quaternion.identity, pathParent.transform);
				yield return new WaitForSeconds(0.2f);
			}
		}
		gameState = GameState.ShowPath;
		yield return null;
	}
	IEnumerator AStarShowPath()
	{
		Pos pos = endPos;
		while (!pos.Equals(startPos))
		{
			Instantiate(prefab_way, new Vector3(pos.x * 1, 0.5f, pos.y * 1), Quaternion.identity, pathParent.transform);
			
				pos = astar_search[pos.y, pos.x].parent;
			
			Debug.Log(pos.x+" "+pos.y);
			yield return new WaitForSeconds(0.1f);
		}
	}
	void Refresh()
	{
		if (GameObject.FindGameObjectsWithTag("Path")!=null)
		{
			// 删除所有格子
			GameObject[] all_go = GameObject.FindGameObjectsWithTag("Path");
			foreach (var go in all_go)
			{
				Destroy(go);
			}
		}

		for (int i=0;i<H;i++) 
		{
			for (int j = 0; j < W; j++)
			{
				if (map[i,j]==START)
				{
					//Debug.Log("START "+ prefab_start);
					var go = Instantiate(prefab_start, new Vector3(j * 1, 0.5f, i * 1), Quaternion.identity, pathParent.transform);
					go.tag = "Path";
				}
				if (map[i, j] == END)
				{
					var go = Instantiate(prefab_end, new Vector3(j * 1, 0.5f, i * 1), Quaternion.identity, pathParent.transform);
					go.tag = "Path";
				}
			}
		}
	}

	void RefreshPath(short[,] temp_map)
	{
		Refresh();
		for (int i = 0; i < H; i++)
		{
			string line = "";
			for (int j = 0; j < W; j++)
			{
				line += temp_map[i, j] + " ";
				if (map[i,j]==0&&temp_map[i,j]!=short.MaxValue)
				{
					var go = Instantiate(prefab_path, new Vector3(j * 1, 0.1f, i * 1), Quaternion.identity, pathParent.transform);
					go.tag = "Path";
				}
			}
		}
	}
}
