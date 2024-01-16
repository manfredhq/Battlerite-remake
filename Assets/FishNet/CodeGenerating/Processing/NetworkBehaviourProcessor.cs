﻿using FishNet.CodeGenerating.Extension;
using FishNet.CodeGenerating.Helping;
using FishNet.CodeGenerating.Helping.Extension;
using FishNet.CodeGenerating.Processing.Rpc;
using FishNet.Configuring;
using FishNet.Object;
using MonoFN.Cecil;
using MonoFN.Cecil.Cil;
using MonoFN.Collections.Generic;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace FishNet.CodeGenerating.Processing
{
    internal class NetworkBehaviourProcessor : CodegenBase
    {
        #region Private.
        /// <summary>
        /// Classes which have been processed for all NetworkBehaviour features.
        /// </summary>
        private HashSet<TypeDefinition> _processedClasses = new HashSet<TypeDefinition>();
        #endregion

        #region Const.
        internal const string EARLY_INITIALIZED_NAME = "NetworkInitializeEarly_";
        internal const string LATE_INITIALIZED_NAME = "NetworkInitializeLate_";
        internal const string NETWORKINITIALIZE_EARLY_INTERNAL_NAME = "NetworkInitialize___Early";
        internal const string NETWORKINITIALIZE_LATE_INTERNAL_NAME = "NetworkInitialize__Late";
        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="typeDef"></param>
        /// <param name="processedSyncs">SyncTypes processed for typeDef and inherited.</param>
        /// <returns></returns>
        internal bool ProcessLocal(TypeDefinition typeDef, List<(SyncType, ProcessedSync)> processedSyncs)
        {
            bool modified = false;
            TypeDefinition copyTypeDef = typeDef;

            //TypeDefs which are using prediction.
            List<TypeDefinition> _usesPredictionTypeDefs = new List<TypeDefinition>();

            //Make collection of NBs to processor.
            List<TypeDefinition> typeDefs = new List<TypeDefinition>();
            do
            {
                if (!HasClassBeenProcessed(copyTypeDef))
                {
                    //Disallow nested network behaviours.
                    ICollection<TypeDefinition> nestedTds = copyTypeDef.NestedTypes;
                    foreach (TypeDefinition item in nestedTds)
                    {
                        if (item.InheritsNetworkBehaviour(base.Session))
                        {
                            base.LogError($"{copyTypeDef.FullName} contains nested NetworkBehaviours. These are not supported.");
                            return modified;
                        }
                    }

                    typeDefs.Add(copyTypeDef);
                }
                copyTypeDef = TypeDefinitionExtensionsOld.GetNextBaseClassToProcess(copyTypeDef, base.Session);
            } while (copyTypeDef != null);

            /* Reverse type definitions so that the parent
             * is first. This counts indexes up as we go further
             * down the children. By doing so we do not have to
             * rebuild rpc or synctype indexes when a parent is inherited
             * multiple times. EG: with this solution if parent had 1 sync type
             * and childA had 2 the parent would be index 0, and childA would have 1 and 2.
             * But also, if childB inherited childA it would have 3+.
             * 
             * Going in reverse also gaurantees the awake method will already be created
            * or modified in any class a child inherits. This lets us call it appropriately
            * as well error if the awake does not exist, such as could not be created. */
            typeDefs.Reverse();

            foreach (TypeDefinition td in typeDefs)
            {
                /* Create NetworkInitialize before-hand so the other procesors
                 * can use it. */
                MethodDefinition networkInitializeIfDisabledMd;
                CreateNetworkInitializeMethods(td, out networkInitializeIfDisabledMd);
                CallNetworkInitializesFromNetworkInitializeIfDisabled(networkInitializeIfDisabledMd);

                //PROSTART
                /* Code stripping. */
                modified |= StripNetworkBehaviourCallbacks(td);
                //PROEND

                /* Prediction. */
                /* Run prediction first since prediction will modify
                 * user data passed into prediction methods. Because of this
                 * other RPCs should use the modified version and reader/writers
                 * made for prediction. */
                if (base.GetClass<PredictionProcessor>().Process(td))
                {
                    _usesPredictionTypeDefs.Add(td);
                    modified = true;
                }
                //25ms 

                /* RPCs. */
                modified |= base.GetClass<RpcProcessor>().ProcessLocal(td);
                //30ms
                /* //perf rpcCounts can be optimized by having different counts
                 * for target, observers, server, replicate, and reoncile rpcs. Since
                 * each registers to their own delegates this is possible. */

                /* SyncTypes. */
                modified |= base.GetClass<NetworkBehaviourSyncProcessor>().ProcessLocal(td, processedSyncs);

                //Call base networkinitialize early/late.
                CallBaseOnNetworkInitializeMethods(td);
                //Add networkinitialize executed check to early/late.
                AddNetworkInitializeExecutedChecks(td);

                //Copy user logic from awake into a new method.
                CopyAwakeUserLogic(td);
                /* Create awake method or if already exist make
                * it public virtual. */
                if (!ModifyAwakeMethod(td, out bool awakeCreated))
                {
                    //This is a hard fail and will break the solution so throw here.
                    base.LogError($"{td.FullName} has an Awake method which could not be modified, or could not be found. This often occurs when a child class is in an assembly different from the parent, and the parent does not implement Awake. To resolve this make an Awake in {td.Name} public virtual.");
                    return modified;
                }

                //Calls NetworkInitializeEarly from awake.
                CallMethodFromAwake(td, NETWORKINITIALIZE_EARLY_INTERNAL_NAME);
                //Only call base if awake was created. Otherwise let the users implementation handle base calling.
                if (awakeCreated)
                    CallBaseAwake(td);
                //Call logic user may have put in awake.
                CallAwakeUserLogic(td);
                //NetworkInitializeLate from awake.
                CallMethodFromAwake(td, NETWORKINITIALIZE_LATE_INTERNAL_NAME);
                //Since awake methods are erased ret has to be added at the end.
                AddReturnToAwake(td);

                //70ms
                _processedClasses.Add(td);
            }

            if (processedSyncs.Count > NetworkBehaviourHelper.MAX_SYNCTYPE_ALLOWANCE)
            {
                base.LogError($"Found {processedSyncs.Count} SyncTypes within {typeDef.FullName} and inherited classes. The maximum number of allowed SyncTypes within type and inherited types is {NetworkBehaviourHelper.MAX_SYNCTYPE_ALLOWANCE}. Remove SyncTypes or condense them using data containers, or a custom SyncObject.");
                return false;
            }

            /* If here then all inerited classes for firstTypeDef have
             * been processed. */

            return modified;
        }

        /// <summary>
        /// Gets the name to use for user awake logic method.
        /// </summary>
        internal string GetAwakeUserLogicMethodDefinition(TypeDefinition td) => $"Awake_UserLogic_{td.FullName}_{base.Module.Name}";

        /// <summary>
        /// Returns if a class has been processed.
        /// </summary>
        /// <param name="typeDef"></param>
        /// <returns></returns>
        private bool HasClassBeenProcessed(TypeDefinition typeDef)
        {
            return _processedClasses.Contains(typeDef);
        }

        /// <summary>
        /// Returns if any typeDefs have attributes which are not allowed to be used outside NetworkBehaviour.
        /// </summary>
        /// <param name="typeDefs"></param>
        /// <returns></returns>
        internal bool NonNetworkBehaviourHasInvalidAttributes(Collection<TypeDefinition> typeDefs)
        {
            NetworkBehaviourSyncProcessor nbSyncProcessor = base.GetClass<NetworkBehaviourSyncProcessor>();
            RpcProcessor rpcProcessor = base.GetClass<RpcProcessor>();

            foreach (TypeDefinition typeDef in typeDefs)
            {
                //Inherits, don't need to check.
                if (typeDef.InheritsNetworkBehaviour(base.Session))
                    continue;

                //Check each method for attribute.
                foreach (MethodDefinition md in typeDef.Methods)
                {
                    //Has RPC attribute but doesn't inherit from NB.
                    if (rpcProcessor.Attributes.HasRpcAttributes(md))
                    {
                        base.LogError($"{typeDef.FullName} has one or more RPC attributes but does not inherit from NetworkBehaviour.");
                        return true;
                    }
                }
                //Check fields for attribute.
                foreach (FieldDefinition fd in typeDef.Fields)
                {
                    if (nbSyncProcessor.IsSyncType(fd))
                    {
                        base.LogError($"{typeDef.FullName} implements one or more SyncTypes but does not inherit from NetworkBehaviour.");
                        return true;
                    }
                }
            }

            //Fallthrough / pass.
            return false;
        }

        //PROSTART
        /// <summary>
        /// Removes NetworkBehaviour callbacks which don't need to be known to client or server.
        /// </summary>
        /// <param name="typeDef"></param>
        /// <returns></returns>
        private bool StripNetworkBehaviourCallbacks(TypeDefinition typeDef)
        {
            bool removeClient = CodeStripping.RemoveClientLogic;
            bool removeServer = CodeStripping.RemoveServerLogic;

            List<MethodDefinition> methods = typeDef.Methods.ToList();
            int startCount = methods.Count;
            foreach (MethodDefinition md in methods)
            {
                if (!md.IsVirtual)
                    continue;

                if (removeClient)
                {
                    if (
                        (md.Name == nameof(NetworkBehaviour.OnStartClient)) ||
                        (md.Name == nameof(NetworkBehaviour.OnStopClient)) ||
                        (md.Name == nameof(NetworkBehaviour.OnOwnershipClient))
                        )
                        typeDef.Methods.Remove(md);
                }
                else if (removeServer)
                {
                    if (
                        (md.Name == nameof(NetworkBehaviour.OnStartServer)) ||
                        (md.Name == nameof(NetworkBehaviour.OnStopServer)) ||
                        (md.Name == nameof(NetworkBehaviour.OnOwnershipServer)) ||
                        (md.Name == nameof(NetworkBehaviour.OnSpawnServer)) ||
                        (md.Name == nameof(NetworkBehaviour.OnDespawnServer))
                        )
                        typeDef.Methods.Remove(md);
                }

            }

            return (typeDef.Methods.Count != startCount);
        }
        //PROEND

        /// <summary>
        /// Calls the next awake method if the nested awake was created by codegen.
        /// </summary>
        /// <returns></returns>
        private void CallBaseAwake(TypeDefinition td)
        {
            /* If base is not a class which can be processed then there
            * is no need to continue. */
            if (!td.CanProcessBaseType(base.Session))
                return;

            MethodReference baseAwakeMr = td.GetMethodReferenceInBase(base.Session, NetworkBehaviourHelper.AWAKE_METHOD_NAME);
            //This Awake.
            MethodDefinition tdAwakeMd = td.GetMethod(NetworkBehaviourHelper.AWAKE_METHOD_NAME);

            ILProcessor processor = tdAwakeMd.Body.GetILProcessor();
            processor.Emit(OpCodes.Ldarg_0); //base.
            processor.Emit(OpCodes.Call, baseAwakeMr);
        }


        /// <summary>
        /// Calls the next awake method if the nested awake was created by codegen.
        /// </summary>
        /// <returns></returns>
        private void CallAwakeUserLogic(TypeDefinition td)
        {
            //UserLogic.
            MethodDefinition userLogicMd = td.GetMethod(GetAwakeUserLogicMethodDefinition(td));
            /* Userlogic may be null if Awake was created.
             * If so, there's no need to proceed. */
            if (userLogicMd == null)
                return;

            //This Awake.
            MethodDefinition awakeMd = td.GetMethod(NetworkBehaviourHelper.AWAKE_METHOD_NAME);
            //Call logic.
            base.GetClass<GeneralHelper>().CallCopiedMethod(awakeMd, userLogicMd);
        }


        /// <summary>
        /// Adds a check to NetworkInitialize to see if it has already run.
        /// </summary>
        /// <param name="typeDef"></param>
        private void AddNetworkInitializeExecutedChecks(TypeDefinition typeDef)
        {
            AddCheck(NETWORKINITIALIZE_EARLY_INTERNAL_NAME);
            AddCheck(NETWORKINITIALIZE_LATE_INTERNAL_NAME);

            void AddCheck(string methodName)
            {
                string fieldName = $"{methodName}{typeDef.FullName}{typeDef.Module.Name}_Excuted";
                MethodDefinition md = typeDef.GetMethod(methodName);
                if (md == null)
                    return;

                TypeReference boolTr = base.GetClass<GeneralHelper>().GetTypeReference(typeof(bool));
                FieldReference fr = typeDef.GetOrCreateFieldReference(base.Session, fieldName, FieldAttributes.Private, boolTr, out bool created);

                if (created)
                {
                    List<Instruction> insts = new List<Instruction>();
                    ILProcessor processor = md.Body.GetILProcessor();
                    //Add check if already called.
                    //if (alreadyInitialized) return;
                    Instruction skipFirstRetInst = processor.Create(OpCodes.Nop);
                    insts.Add(processor.Create(OpCodes.Ldarg_0));
                    insts.Add(processor.Create(OpCodes.Ldfld, fr));
                    insts.Add(processor.Create(OpCodes.Brfalse_S, skipFirstRetInst));
                    insts.Add(processor.Create(OpCodes.Ret));
                    insts.Add(skipFirstRetInst);
                    //Set field to true.
                    insts.Add(processor.Create(OpCodes.Ldarg_0));
                    insts.Add(processor.Create(OpCodes.Ldc_I4_1));
                    insts.Add(processor.Create(OpCodes.Stfld, fr));
                    processor.InsertFirst(insts);
                }
            }
        }

        /// <summary>
        /// Calls base for NetworkInitializeEarly/Late on a TypeDefinition.
        /// </summary>
        private void CallBaseOnNetworkInitializeMethods(TypeDefinition typeDef)
        {
            //If base class cannot have a networkinitialize no reason to continue.
            if (!typeDef.CanProcessBaseType(base.Session))
                return;

            string[] initializeMethodNames = new string[] { NETWORKINITIALIZE_EARLY_INTERNAL_NAME, NETWORKINITIALIZE_LATE_INTERNAL_NAME };
            foreach (string mdName in initializeMethodNames)
            {
                /* Awake will always exist because it was added previously.
                * Get awake for copy and base of copy. */
                MethodDefinition thisMd = typeDef.GetMethod(mdName);
                ILProcessor processor = thisMd.Body.GetILProcessor();

                /* Awake will always exist because it was added previously.
                 * Get awake for copy and base of copy. */
                MethodReference baseMr = typeDef.GetMethodReferenceInBase(base.Session, mdName);
                MethodDefinition baseMd = baseMr.CachedResolve(base.Session);

                bool alreadyHasBaseCall = false;
                //Check if already calls baseAwake.
                foreach (Instruction item in thisMd.Body.Instructions)
                {
                    //If a call or call virt. Although, callvirt should never occur.
                    if (item.OpCode == OpCodes.Call || item.OpCode == OpCodes.Callvirt)
                    {
                        if (item.Operand != null && item.Operand.GetType().Name == nameof(MethodDefinition))
                        {
                            MethodDefinition md = (MethodDefinition)item.Operand;
                            if (md == baseMd)
                            {
                                alreadyHasBaseCall = true;
                                break;
                            }
                        }
                    }
                }

                if (!alreadyHasBaseCall)
                {
                    //Create instructions for base call.
                    List<Instruction> instructions = new List<Instruction>
                    {
                        processor.Create(OpCodes.Ldarg_0), //this.
                        processor.Create(OpCodes.Call, baseMr)
                    };
                    processor.InsertFirst(instructions);
                }
            }
        }

        /// <summary>
        /// Adds returns awake method definitions within awakeDatas.
        /// </summary>
        private void AddReturnToAwake(TypeDefinition td)
        {
            //This Awake.
            MethodDefinition awakeMd = td.GetMethod(NetworkBehaviourHelper.AWAKE_METHOD_NAME);
            ILProcessor processor = awakeMd.Body.GetILProcessor();
            //If no instructions or the last instruction isnt ret.
            if (processor.Body.Instructions.Count == 0
                || processor.Body.Instructions[processor.Body.Instructions.Count - 1].OpCode != OpCodes.Ret)
            {
                processor.Emit(OpCodes.Ret);
            }
        }

        /// <summary>
        /// Calls a method by name from awake.
        /// </summary>
        private void CallMethodFromAwake(TypeDefinition typeDef, string methodName)
        {
            //Will never be null because we added it previously.
            MethodDefinition awakeMethodDef = typeDef.GetMethod(NetworkBehaviourHelper.AWAKE_METHOD_NAME);
            MethodReference networkInitMr = typeDef.GetMethodReference(base.Session, methodName);

            ILProcessor processor = awakeMethodDef.Body.GetILProcessor();
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(networkInitMr.GetCallOpCode(base.Session), networkInitMr);
        }

        /// <summary>
        /// Creates an 'NetworkInitialize' method which is called by the childmost class to initialize scripts on Awake.
        /// </summary>
        private void CreateNetworkInitializeMethods(TypeDefinition typeDef, out MethodDefinition networkInitializeIfDisabledMd)
        {
            CreateMethod(NETWORKINITIALIZE_EARLY_INTERNAL_NAME);
            CreateMethod(NETWORKINITIALIZE_LATE_INTERNAL_NAME);
            networkInitializeIfDisabledMd = CreateMethod(nameof(NetworkBehaviour.NetworkInitializeIfDisabled));

            MethodDefinition CreateMethod(string name, MethodDefinition copied = null)
            {
                bool created;
                MethodDefinition md = typeDef.GetOrCreateMethodDefinition(base.Session, name, MethodDefinitionExtensions.PUBLIC_VIRTUAL_ATTRIBUTES, typeDef.Module.TypeSystem.Void, out created);

                if (created)
                {
                    //Emit ret into new method.
                    ILProcessor processor = md.Body.GetILProcessor();
                    //End of method return.
                    processor.Emit(OpCodes.Ret);
                }

                return md;
            }
        }


        /// <summary>
        /// Creates an 'NetworkInitialize' method which is called by the childmost class to initialize scripts on Awake.
        /// </summary>
        private void CallNetworkInitializesFromNetworkInitializeIfDisabled(MethodDefinition networkInitializeIfDisabledMd)
        {
            ILProcessor processor = networkInitializeIfDisabledMd.Body.GetILProcessor();

            networkInitializeIfDisabledMd.Body.Instructions.Clear();
            CallMethod(NETWORKINITIALIZE_EARLY_INTERNAL_NAME);
            CallMethod(NETWORKINITIALIZE_LATE_INTERNAL_NAME);
            processor.Emit(OpCodes.Ret);

            void CallMethod(string name)
            {
                MethodReference initIfDisabledMr = networkInitializeIfDisabledMd.DeclaringType.GetMethodReference(base.Session, name);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(initIfDisabledMr.GetCallOpCode(base.Session), initIfDisabledMr);
            }
        }

        /// <summary>
        /// Copies logic from users Awake if present, to a new method.
        /// </summary>
        private void CopyAwakeUserLogic(TypeDefinition typeDef)
        {
            MethodDefinition awakeMd = typeDef.GetMethod(NetworkBehaviourHelper.AWAKE_METHOD_NAME);
            //If found copy.
            if (awakeMd != null)
                base.GetClass<GeneralHelper>().CopyIntoNewMethod(awakeMd, GetAwakeUserLogicMethodDefinition(typeDef), out _);
        }

        /// <summary>
        /// Erases content in awake if it already exist, otherwise makes a new Awake.
        /// Makes Awake public and virtual.
        /// </summary>
        /// <returns>True if successful.</returns>
        private bool ModifyAwakeMethod(TypeDefinition typeDef, out bool created)
        {
            MethodDefinition awakeMd = typeDef.GetOrCreateMethodDefinition(base.Session, NetworkBehaviourHelper.AWAKE_METHOD_NAME, MethodDefinitionExtensions.PUBLIC_VIRTUAL_ATTRIBUTES, typeDef.Module.TypeSystem.Void, out created);

            //Awake is found. Check for invalid return type.
            if (!created)
            {
                if (awakeMd.ReturnType != typeDef.Module.TypeSystem.Void)
                {
                    base.LogError($"IEnumerator Awake methods are not supported within NetworkBehaviours.");
                    return false;
                }
                //Make public if good.
                awakeMd.SetPublicAttributes();
            }
            //Already was made.
            else
            {
                ILProcessor processor = awakeMd.Body.GetILProcessor();
                processor.Emit(OpCodes.Ret);
            }

            //Clear original awake.
            awakeMd.Body.Instructions.Clear();

            return true;
        }

        /// <summary>
        /// Makes all Awake methods within typeDef and base classes public and virtual.
        /// </summary>
        /// <param name="typeDef"></param>
        internal void CreateFirstNetworkInitializeCall(TypeDefinition typeDef, MethodDefinition firstUserAwakeMethodDef, MethodDefinition firstNetworkInitializeMethodDef)
        {
            ILProcessor processor;
            //Get awake for current method.
            MethodDefinition thisAwakeMethodDef = typeDef.GetMethod(NetworkBehaviourHelper.AWAKE_METHOD_NAME);
            bool created = false;

            //If no awake then make one.
            if (thisAwakeMethodDef == null)
            {
                created = true;

                thisAwakeMethodDef = new MethodDefinition(NetworkBehaviourHelper.AWAKE_METHOD_NAME, MethodDefinitionExtensions.PUBLIC_VIRTUAL_ATTRIBUTES,
                    typeDef.Module.TypeSystem.Void);
                thisAwakeMethodDef.Body.InitLocals = true;
                typeDef.Methods.Add(thisAwakeMethodDef);

                processor = thisAwakeMethodDef.Body.GetILProcessor();
                processor.Emit(OpCodes.Ret);
            }

            //MethodRefs for networkinitialize and awake.
            MethodReference networkInitializeMethodRef = typeDef.Module.ImportReference(firstNetworkInitializeMethodDef);

            processor = thisAwakeMethodDef.Body.GetILProcessor();
            //Create instructions for base call.
            List<Instruction> instructions = new List<Instruction>
            {
                processor.Create(OpCodes.Ldarg_0), //this.
                processor.Create(OpCodes.Call, networkInitializeMethodRef)
            };

            /* If awake was created then make a call to the users
             * first awake. There's no reason to do this if awake
             * already existed because the user would have control
             * over making that call. */
            if (created && firstUserAwakeMethodDef != null)
            {
                MethodReference baseAwakeMethodRef = typeDef.Module.ImportReference(firstUserAwakeMethodDef);
                instructions.Add(processor.Create(OpCodes.Ldarg_0));//this.
                instructions.Add(processor.Create(OpCodes.Call, baseAwakeMethodRef));
            }

            processor.InsertFirst(instructions);
        }


    }
}