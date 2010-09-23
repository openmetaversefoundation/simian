/*
 * Copyright (c) Open Metaverse Foundation
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions
 * are met:
 * 1. Redistributions of source code must retain the above copyright
 *    notice, this list of conditions and the following disclaimer.
 * 2. Redistributions in binary form must reproduce the above copyright
 *    notice, this list of conditions and the following disclaimer in the
 *    documentation and/or other materials provided with the distribution.
 * 3. The name of the author may not be used to endorse or promote products
 *    derived from this software without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE AUTHOR ``AS IS'' AND ANY EXPRESS OR
 * IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
 * OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
 * IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY DIRECT, INDIRECT,
 * INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT
 * NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
 * DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
 * THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF
 * THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using Tools;

namespace Simian.Scripting.Linden
{
    public class LSL2CSCodeTransformer
    {
        private SYMBOL m_astRoot;
        private static Dictionary<string, string> m_datatypeLSL2CS;

        /// <summary>
        /// Pass the new CodeTranformer an abstract syntax tree.
        /// </summary>
        /// <param name="astRoot">The root node of the AST.</param>
        public LSL2CSCodeTransformer(SYMBOL astRoot)
        {
            m_astRoot = astRoot;

            m_datatypeLSL2CS = new Dictionary<string, string>()
            {
                { "integer", "lsl_integer" },
                { "float", "lsl_float" },
                { "key", "lsl_string" },
                { "string", "lsl_string" },
                { "vector", "lsl_vector" },
                { "rotation", "lsl_rotation" },
                { "list", "lsl_list" }
            };
        }

        /// <summary>
        /// Transform the LSL AST to a C# AST
        /// </summary>
        /// <returns>The root node of the transformed AST</returns>
        public SYMBOL Transform()
        {
            foreach (SYMBOL s in m_astRoot.kids)
                TransformNode(s);

            return m_astRoot;
        }

        /// <summary>
        /// Recursively called to transform each type of node. Will transform this
        /// node, then all of its children
        /// </summary>
        /// <param name="s">The current node to transform</param>
        private void TransformNode(SYMBOL s)
        {
            // make sure to put type lower in the inheritance hierarchy first
            // ie: since IdentConstant and StringConstant inherit from Constant,
            // put IdentConstant and StringConstant before Constant
            if (s is Declaration)
                ((Declaration)s).Datatype = m_datatypeLSL2CS[((Declaration)s).Datatype];
            else if (s is Constant)
                ((Constant)s).Type = m_datatypeLSL2CS[((Constant)s).Type];
            else if (s is TypecastExpression)
                ((TypecastExpression)s).TypecastType = m_datatypeLSL2CS[((TypecastExpression)s).TypecastType];
            else if (s is GlobalFunctionDefinition && "void" != ((GlobalFunctionDefinition)s).ReturnType) // we don't need to translate "void"
                ((GlobalFunctionDefinition)s).ReturnType = m_datatypeLSL2CS[((GlobalFunctionDefinition)s).ReturnType];

            for (int i = 0; i < s.kids.Count; i++)
            {
                if (!(s is Assignment || s is ArgumentDeclarationList) && s.kids[i] is Declaration)
                    AddImplicitInitialization(s, i);

                TransformNode((SYMBOL)s.kids[i]);
            }
        }

        /// <summary>
        /// Replaces an instance of the node at s.kids[didx] with an assignment
        /// node. The assignment node has the Declaration node on the left hand
        /// side and a default initializer on the right hand side
        /// </summary>
        /// <param name="s">
        /// The node containing the Declaration node that needs replacing
        /// </param>
        /// <param name="didx">Index of the Declaration node to replace</param>
        private void AddImplicitInitialization(SYMBOL s, int didx)
        {
            // We take the kids for a while to play with them
            int sKidSize = s.kids.Count;
            object[] sKids = new object[sKidSize];
            for (int i = 0; i < sKidSize; i++)
                sKids[i] = s.kids.Pop();

            // The child to be changed
            Declaration currentDeclaration = (Declaration)sKids[didx];

            // We need an assignment node
            Assignment newAssignment = new Assignment(currentDeclaration.yyps,
                                                      currentDeclaration,
                                                      GetZeroConstant(currentDeclaration.yyps, currentDeclaration.Datatype),
                                                      "=");
            sKids[didx] = newAssignment;

            // Put the kids back where they belong
            for (int i = 0; i < sKidSize; i++)
                s.kids.Add(sKids[i]);
        }

        /// <summary>
        /// Generates the node structure required to generate a default
        /// initialization
        /// </summary>
        /// <param name="p">
        /// Tools.Parser instance to use when instantiating nodes
        /// </param>
        /// <param name="constantType">String describing the datatype</param>
        /// <returns>
        /// A SYMBOL node conaining the appropriate structure for intializing a
        /// constantType
        /// </returns>
        private SYMBOL GetZeroConstant(Parser p, string constantType)
        {
            switch (constantType)
            {
                case "integer":
                    return new Constant(p, constantType, "0");
                case "float":
                    return new Constant(p, constantType, "0.0");
                case "string":
                case "key":
                    return new Constant(p, constantType, "");
                case "list":
                    ArgumentList al = new ArgumentList(p);
                    return new ListConstant(p, al);
                case "vector":
                    Constant vca = new Constant(p, "float", "0.0");
                    Constant vcb = new Constant(p, "float", "0.0");
                    Constant vcc = new Constant(p, "float", "0.0");
                    ConstantExpression vcea = new ConstantExpression(p, vca);
                    ConstantExpression vceb = new ConstantExpression(p, vcb);
                    ConstantExpression vcec = new ConstantExpression(p, vcc);
                    return new VectorConstant(p, vcea, vceb, vcec);
                case "rotation":
                    Constant rca = new Constant(p, "float", "0.0");
                    Constant rcb = new Constant(p, "float", "0.0");
                    Constant rcc = new Constant(p, "float", "0.0");
                    Constant rcd = new Constant(p, "float", "0.0");
                    ConstantExpression rcea = new ConstantExpression(p, rca);
                    ConstantExpression rceb = new ConstantExpression(p, rcb);
                    ConstantExpression rcec = new ConstantExpression(p, rcc);
                    ConstantExpression rced = new ConstantExpression(p, rcd);
                    return new RotationConstant(p, rcea, rceb, rcec, rced);
                default:
                    return null; // TODO: This will probably break things
            }
        }
    }
}
