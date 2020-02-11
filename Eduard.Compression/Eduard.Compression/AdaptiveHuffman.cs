/*
 BSD 3-Clause License

 Copyright (c) 2020, Matei Ionut-Eduard
 All rights reserved.

 Redistribution and use in source and binary forms, with or without
 modification, are permitted provided that the following conditions are met:

 1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.

 2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

 3. Neither the name of the copyright holder nor the names of its
   contributors may be used to endorse or promote products derived from
   this software without specific prior written permission.

 THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
 FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
 DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
 SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
 CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
 OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
 OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Eduard.Compression
{
    internal unsafe class AdaptiveHuffman
    {
        struct Node
        {
            public int isZero;
            public int isRoot;
            public int isLeaf;

            public Node* parent;
            public Node* leftChild;
            public Node* rightChild;

            public int symbol;
            public int value;
            public int order;
        }

        struct Table
        {
            public int symbol;
            public Node* tree;
        }

        private static Node* AddChild(Node* parent, int isZero, int isRoot, int symbol, int value, int order)
        {
            Node* node = (Node*)Marshal.AllocHGlobal(sizeof(Node));
            node->isZero = isZero;
            node->isRoot = isRoot;
            node->isLeaf = 1;

            node->parent = parent;
            node->leftChild = null;
            node->rightChild = null;
            node->symbol = symbol;

            node->value = value;
            node->order = order;
            return node;
        }

        private static Node* AddSymbol(int symbol, Node** zeroNode, Table** symbols)
        {
            Node* leftNode = AddChild(*zeroNode, 1, 0, -1, 0, (*zeroNode)->order - 2);
            Node* rightNode = AddChild(*zeroNode, 0, 0, symbol, 1, (*zeroNode)->order - 1);

            Node* previousZeroNode = *zeroNode;
            (*zeroNode)->isZero = 0;
            (*zeroNode)->isLeaf = 0;
            (*zeroNode)->leftChild = leftNode;
            (*zeroNode)->rightChild = rightNode;

            int symbolIndex = symbol;
            symbols[symbolIndex] = (Table*)Marshal.AllocHGlobal(sizeof(Table));
            symbols[symbolIndex]->symbol = symbol;
            symbols[symbolIndex]->tree = rightNode;

            *zeroNode = leftNode;
            return previousZeroNode;
        }

        private static Node* FindReplaceNode(Node* currMax, Node* root)
        {
            Node* result = currMax;

            if (root->value > result->value && root->isLeaf == 0)
            {
                Node* greatestLeft = FindReplaceNode(result, root->leftChild);
                if (greatestLeft != null) result = greatestLeft;

                Node* greatestRight = FindReplaceNode(result, root->rightChild);
                if (greatestRight != null) result = greatestRight;
            }
            else if (root->value == result->value && root->order > result->order)
                result = root;

            return (result != currMax) ? result : null;
        }

        private static void SwapNodes(Node* n1, Node* n2)
        {
            int tempOrder = n1->order;
            n1->order = n2->order;
            n2->order = tempOrder;

            if (n1->parent->leftChild == n1)
                n1->parent->leftChild = n2;
            else
                n1->parent->rightChild = n2;

            if (n2->parent->leftChild == n2)
                n2->parent->leftChild = n1;
            else
                n2->parent->rightChild = n1;

            Node* temp = n1->parent;
            n1->parent = n2->parent;
            n2->parent = temp;
        }

        private static void UpdateTree(Node* currNode, Node* root)
        {
            while (currNode->isRoot == 0)
            {
                Node* replaceNode = FindReplaceNode(currNode, root);

                if (replaceNode != null && currNode->parent != replaceNode)
                    SwapNodes(currNode, replaceNode);

                currNode->value++;
                currNode = currNode->parent;
            }

            currNode->value++;
        }

        private static void WriteBuffer(BinaryStream bs, Node* node)
        {
            Node* ptr = node;
            List<int> list = new List<int>();

            while(ptr->isRoot == 0)
            {
                Node* daddy = ptr->parent;
                int bit = (daddy->rightChild == ptr) ? 1 : 0;
                list.Add(bit);
                ptr = ptr->parent;
            }

            list.Reverse();

            foreach (int bit in list)
                bs.WriteBit(bit);

            list.Clear();
        }

        private static void DestroyTree(Node* node)
        {
            if (node->leftChild != null)
                DestroyTree(node->leftChild);

            if(node->rightChild != null)
                DestroyTree(node->rightChild);

            Marshal.FreeHGlobal((IntPtr)node);
        }

        public static byte[] Compress(byte[] data)
        {
            MemoryStream inner = new MemoryStream(data);
            MemoryStream outter = new MemoryStream();
            BinaryStream bs = new BinaryStream(outter);

            Node* root = CreateTree();
            Node* zeroNode = root;
            byte currByte;

            Table** table = (Table**)Marshal.AllocHGlobal(257 * sizeof(Table*));
            Node* node = AddSymbol(256, &zeroNode, table);

            UpdateTree(node, root);
            Node* end = lookup(256, table);

            for (int i = 0; i < 256; i++)
                table[i] = null;

            while (inner.Position < inner.Length) {
                currByte = (byte)inner.ReadByte();
                Node* symbolTree = lookup(currByte, table);
                
                if (symbolTree != null)
                {
                    WriteBuffer(bs, symbolTree);
                    UpdateTree(symbolTree, root);
                }
                else
                {
                    WriteBuffer(bs, zeroNode);
                    bs.WriteByte(currByte);
                    Node* newNode = AddSymbol(currByte, &zeroNode, table);
                    UpdateTree(newNode, root);
                }
            }

            WriteBuffer(bs, end);
            bs.Flush();
            DestroyTree(root);

            Marshal.FreeHGlobal((IntPtr)table);
            return outter.ToArray();
        }

        public static byte[] Extract(Stream stream)
        {
            MemoryStream outter = new MemoryStream();
            BinaryStream bs = new BinaryStream(stream);

            Node* root = CreateTree();
            Node* zeroNode = root;

            Table** table = (Table**)Marshal.AllocHGlobal(257 * sizeof(Table*));
            Node* node = AddSymbol(256, &zeroNode, table);

            UpdateTree(node, root);
            Node* end = lookup(256, table);

            for (int i = 0; i < 256; i++)
                table[i] = null;

            while (stream.Position < stream.Length)
            {
                Node* currNode = root;
                bool endOfFile = false;

                while (currNode->isLeaf == 0 && !endOfFile)
                {
                    int bit = bs.ReadBit();

                    if (bit == 0)
                        currNode = currNode->leftChild;
                    else if (bit == 1)
                        currNode = currNode->rightChild;
                    else
                        endOfFile = true;
                }

                if (endOfFile) break;

                int ch;

                if (currNode->isZero == 1)
                {
                    ch = bs.ReadByte();
                    currNode = AddSymbol(ch, &zeroNode, table);
                }
                else
                    ch = currNode->symbol;

                if (ch == 256) break;
                outter.WriteByte((byte)ch);
                UpdateTree(currNode, root);
            }

            DestroyTree(root);
            Marshal.FreeHGlobal((IntPtr)table);
            return outter.ToArray();
        }

        private static Node* CreateTree()
        {
            Node* tree = (Node*)Marshal.AllocHGlobal(sizeof(Node));
            tree->isZero = 1;
            tree->isRoot = 1;
            tree->isLeaf = 1;

            tree->parent = null;
            tree->leftChild = null;
            tree->rightChild = null;

            tree->symbol = -1;
            tree->value = 0;
            tree->order = 512;

            return tree;
        }

        private static Node* lookup(int symbol, Table** table)
        {
            Table* symbolPtr = table[symbol];
            if (symbolPtr == null) return null;
            return symbolPtr->tree;
        }
    }
}
