using System;
using System.Collections.Generic;
using Xwt.Backends;

namespace Xwt.GtkBackend
{
	public class TreeStoreBackend : ITreeStoreBackend
	{
		struct Node {
			public object[] Data;
			public NodeList Children;
			public int NodeId;
		}

		class NodePosition : TreePosition
		{
			public NodeList ParentList;
			public int NodeIndex;
			public int NodeId;
			public int StoreVersion;

			public override bool Equals(object obj)
			{
				var other = obj as NodePosition;
				if (other == null)
					return false;
				return ParentList == other.ParentList && NodeId == other.NodeId;
			}

			public override int GetHashCode()
			{
				return ParentList.GetHashCode() ^ NodeId;
			}
		}

		class NodeList : List<Node>
		{
			public NodePosition Parent;
		}

		Type[] columnTypes;
		NodeList rootNodes = new NodeList();
		int version;
		int nextNodeId;

		public event EventHandler<TreeNodeEventArgs> NodeInserted;
		public event EventHandler<TreeNodeChildEventArgs> NodeDeleted;
		public event EventHandler<TreeNodeEventArgs> NodeChanged;
		public event EventHandler<TreeNodeOrderEventArgs> NodesReordered;
		public event EventHandler Cleared;

		public void InitializeBackend(object frontend, ApplicationContext context)
		{
		}

		public void EnableEvent(object eventId)
		{
		}

		public void DisableEvent(object eventId)
		{
		}

		public void Initialize(Type[] columnTypes)
		{
			this.columnTypes = columnTypes;
		}

		public void Clear()
		{
			rootNodes.Clear();
			Cleared?.Invoke(this, EventArgs.Empty);
		}

		NodePosition GetPosition(TreePosition pos)
		{
			if (pos == null)
				return null;
			var np = (NodePosition)pos;
			if (np.StoreVersion != version) {
				np.NodeIndex = -1;
				for (int i = 0; i < np.ParentList.Count; i++) {
					if (np.ParentList[i].NodeId == np.NodeId) {
						np.NodeIndex = i;
						break;
					}
				}
				if (np.NodeIndex == -1)
					throw new InvalidOperationException("Invalid node position");
				np.StoreVersion = version;
			}
			return np;
		}

		public void SetValue(TreePosition pos, int column, object value)
		{
			var n = GetPosition(pos);
			var node = n.ParentList[n.NodeIndex];
			if (node.Data == null) {
				node.Data = new object[columnTypes.Length];
				n.ParentList[n.NodeIndex] = node;
			}
			node.Data[column] = value;
			NodeChanged?.Invoke(this, new TreeNodeEventArgs(pos, n.NodeIndex));
		}

		public object GetValue(TreePosition pos, int column)
		{
			var np = GetPosition(pos);
			var n = np.ParentList[np.NodeIndex];
			if (n.Data == null)
				return null;
			return n.Data[column];
		}

		public TreePosition GetChild(TreePosition pos, int index)
		{
			if (pos == null) {
				if (rootNodes.Count == 0)
					return null;
				var n = rootNodes[index];
				return new NodePosition { ParentList = rootNodes, NodeId = n.NodeId, NodeIndex = index, StoreVersion = version };
			}

			var np = GetPosition(pos);
			var node = np.ParentList[np.NodeIndex];
			if (node.Children == null || index >= node.Children.Count)
				return null;
			return new NodePosition { ParentList = node.Children, NodeId = node.Children[index].NodeId, NodeIndex = index, StoreVersion = version };
		}

		public TreePosition GetNext(TreePosition pos)
		{
			var np = GetPosition(pos);
			if (np.NodeIndex >= np.ParentList.Count - 1)
				return null;
			var n = np.ParentList[np.NodeIndex + 1];
			return new NodePosition { ParentList = np.ParentList, NodeId = n.NodeId, NodeIndex = np.NodeIndex + 1, StoreVersion = version };
		}

		public TreePosition GetPrevious(TreePosition pos)
		{
			var np = GetPosition(pos);
			if (np.NodeIndex <= 0)
				return null;
			var n = np.ParentList[np.NodeIndex - 1];
			return new NodePosition { ParentList = np.ParentList, NodeId = n.NodeId, NodeIndex = np.NodeIndex - 1, StoreVersion = version };
		}

		public int GetChildrenCount(TreePosition pos)
		{
			if (pos == null)
				return rootNodes.Count;

			var np = GetPosition(pos);
			var n = np.ParentList[np.NodeIndex];
			return n.Children != null ? n.Children.Count : 0;
		}

		public TreePosition InsertBefore(TreePosition pos)
		{
			var np = GetPosition(pos);
			var nn = new Node { NodeId = nextNodeId++ };

			np.ParentList.Insert(np.NodeIndex, nn);
			version++;

			np.NodeIndex++;
			np.StoreVersion = version;

			var node = new NodePosition { ParentList = np.ParentList, NodeId = nn.NodeId, NodeIndex = np.NodeIndex - 1, StoreVersion = version };
			NodeInserted?.Invoke(this, new TreeNodeEventArgs(node, node.NodeIndex));
			return node;
		}

		public TreePosition InsertAfter(TreePosition pos)
		{
			var np = GetPosition(pos);
			var nn = new Node { NodeId = nextNodeId++ };

			np.ParentList.Insert(np.NodeIndex + 1, nn);
			version++;
			np.StoreVersion = version;

			var node = new NodePosition { ParentList = np.ParentList, NodeId = nn.NodeId, NodeIndex = np.NodeIndex + 1, StoreVersion = version };
			NodeInserted?.Invoke(this, new TreeNodeEventArgs(node, node.NodeIndex));
			return node;
		}

		public TreePosition AddChild(TreePosition pos)
		{
			var np = GetPosition(pos);
			var nn = new Node { NodeId = nextNodeId++ };
			NodeList list;

			if (pos == null) {
				list = rootNodes;
			} else {
				var n = np.ParentList[np.NodeIndex];
				if (n.Children == null) {
					n.Children = new NodeList();
					n.Children.Parent = new NodePosition { ParentList = np.ParentList, NodeId = n.NodeId, NodeIndex = np.NodeIndex, StoreVersion = version };
					np.ParentList[np.NodeIndex] = n;
				}
				list = n.Children;
			}
			list.Add(nn);
			version++;

			if (np != null)
				np.StoreVersion = version;

			var node = new NodePosition { ParentList = list, NodeId = nn.NodeId, NodeIndex = list.Count - 1, StoreVersion = version };
			NodeInserted?.Invoke(this, new TreeNodeEventArgs(node, node.NodeIndex));
			return node;
		}

		public void Remove(TreePosition pos)
		{
			if (pos == null)
				return;
			for (int i = GetChildrenCount(pos) - 1; i >= 0; i--)
				Remove(GetChild(pos, i));
			var np = GetPosition(pos);
			np.ParentList.RemoveAt(np.NodeIndex);
			var parent = np.ParentList.Parent;
			var index = np.NodeIndex;
			version++;
			NodeDeleted?.Invoke(this, new TreeNodeChildEventArgs(parent, index, pos));
		}

		public TreePosition GetParent(TreePosition pos)
		{
			var np = GetPosition(pos);
			if (np.ParentList == rootNodes)
				return null;
			var parent = np.ParentList.Parent;
			return new NodePosition { ParentList = parent.ParentList, NodeId = parent.NodeId, NodeIndex = parent.NodeIndex, StoreVersion = version };
		}

		public Type[] ColumnTypes {
			get { return columnTypes; }
		}

		public void SortNodes(TreePosition parent, Comparison<TreePosition> comparison)
		{
			var list = GetNodes(parent);
			list.Sort((n1, n2) => comparison(CreatePosition(list, n1), CreatePosition(list, n2)));
			version++;
			NodesReordered?.Invoke(this, new TreeNodeOrderEventArgs(parent, GetOrderList(list)));
		}

		public void MoveNode(TreePosition node, TreePosition target, bool before)
		{
			var np = GetPosition(node);
			var item = np.ParentList[np.NodeIndex];
			np.ParentList.RemoveAt(np.NodeIndex);

			if (target != null) {
				var tp = GetPosition(target);
				np.ParentList = tp.ParentList;
				np.NodeIndex = tp.NodeIndex;
				if (!before)
					np.NodeIndex++;
			} else {
				np.ParentList = rootNodes;
				np.NodeIndex = np.ParentList.Count;
			}

			if (np.NodeIndex < 0)
				np.NodeIndex = 0;
			if (np.NodeIndex > np.ParentList.Count)
				np.NodeIndex = np.ParentList.Count;

			np.ParentList.Insert(np.NodeIndex, item);
			version++;

			var parent = np.ParentList.Parent;
			NodesReordered?.Invoke(this, new TreeNodeOrderEventArgs(parent, GetOrderList(np.ParentList)));
		}

		public TreePosition CreateTreePosition(int[] indices)
		{
			TreePosition pos = null;
			for (int i = 0; i < indices.Length; i++) {
				pos = GetChild(pos, indices[i]);
				if (pos == null)
					return null;
			}
			return pos;
		}

		NodeList GetNodes(TreePosition parent)
		{
			if (parent == null)
				return rootNodes;
			var np = GetPosition(parent);
			var n = np.ParentList[np.NodeIndex];
			if (n.Children == null) {
				n.Children = new NodeList();
				n.Children.Parent = new NodePosition { ParentList = np.ParentList, NodeId = n.NodeId, NodeIndex = np.NodeIndex, StoreVersion = version };
				np.ParentList[np.NodeIndex] = n;
			}
			return n.Children;
		}

		NodePosition CreatePosition(NodeList list, Node node)
		{
			var index = list.IndexOf(node);
			return new NodePosition { ParentList = list, NodeId = node.NodeId, NodeIndex = index, StoreVersion = version };
		}

		static int[] GetOrderList(NodeList list)
		{
			var order = new int[list.Count];
			for (int i = 0; i < list.Count; i++)
				order[i] = i;
			return order;
		}
	}
}
