using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CreditTreeCLI;
using CreditTreeCLI.Persistence;

class Program
{
    static CreditTreeRepository? repository;
    static CreditNode? activeRootNode;

    static void Main(string[] args)
    {
        Console.WriteLine("Sistema de Controle de Crédito Hierárquico CLI (v6 - Templates & Multi-Tree)");
        Console.WriteLine("--------------------------------------------------------------------------");

        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

        string dbPath = Path.Combine(AppContext.BaseDirectory, "CreditTreeData_SingleCollection_V2.db");
        Console.WriteLine($"Usando banco de dados: {dbPath}");

        try
        {
            repository = new CreditTreeRepository(dbPath);
            Console.WriteLine("Repositório inicializado.");
            ShowInitialMenu();

            string? command;
            bool treeChanged = false;
            do
            {
                string prompt = activeRootNode != null ? $"[{activeRootNode.Name} ({activeRootNode.TreeId})] > " : "[Nenhuma árvore selecionada] > ";
                Console.Write($"\n{prompt}");
                command = Console.ReadLine()?.Trim().ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(command) || command == "sair") continue;

                try
                {
                    treeChanged = ProcessCommand(command);

                    if (treeChanged && activeRootNode != null && repository != null)
                    {
                        Console.WriteLine($"Salvando alterações na árvore '{activeRootNode.Name}'...");
                        repository.SaveTree(activeRootNode);
                        Console.WriteLine("Salvo.");
                        treeChanged = false;
                    }

                     if(actionRequiresDisplay(command)) // Only display if not a list/help command
                     {
                         Console.WriteLine("\n--- Estado Atual da Árvore Ativa ---");
                         activeRootNode?.DisplayNodeInfo();
                     }

                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"ERRO: {ex.GetType().Name} - {ex.Message}");
                     if (ex is KeyNotFoundException) Console.WriteLine("Verifique se o nome do nó ou ID da árvore está correto.");
                    Console.ResetColor();
                    treeChanged = false;
                }

            } while (command != "sair");
        }
        catch (Exception ex)
        {
             Console.ForegroundColor = ConsoleColor.Red;
             Console.WriteLine($"ERRO FATAL ao inicializar ou no loop principal: {ex.Message}");
             Console.ResetColor();
        }
        finally
        {
             Console.WriteLine("Encerrando aplicação.");
             repository?.Dispose();
        }
    }

     static bool actionRequiresDisplay(string action) {
         return action switch
         {
             "list-templates" => false,
             "list-trees" => false,
             "ajuda" => false,
             "select-tree" => false, // select already shows info
             _ => true
         };
     }

     static void ShowInitialMenu()
     {
         Console.WriteLine("\nOpções Iniciais:");
         Console.WriteLine(" list-templates             - Lista os modelos de árvore disponíveis.");
         Console.WriteLine(" list-trees                 - Lista as árvores de cliente existentes.");
         Console.WriteLine(" create-template            - Inicia a criação de um novo modelo de árvore.");
         Console.WriteLine(" create-tree <templateId> <clienteId> - Cria árvore para cliente a partir de um modelo.");
         Console.WriteLine(" select-tree <treeId>       - Seleciona uma árvore (cliente ou template) para operar.");
         Console.WriteLine(" ajuda                      - Mostra todos os comandos.");
         Console.WriteLine(" sair                       - Encerra a aplicação.");
     }

    static bool ProcessCommand(string command)
    {
        string[] parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return false;

        string action = parts[0];
        string[] args = parts.Skip(1).ToArray();
        bool changed = false;

         switch (action)
         {
             case "ajuda": ShowHelp(); return false;
             case "list-templates": ListTemplatesCommand(); return false;
             case "list-trees": ListCustomerTreesCommand(); return false;
             case "create-template": CreateTemplateCommand(); return true;
             case "create-tree": CreateTreeCommand(args); return true;
             case "select-tree": SelectTreeCommand(args); return false;
             case "sair": return false;
         }

         if (activeRootNode == null)
         {
             Console.WriteLine("ERRO: Nenhuma árvore selecionada. Use 'select-tree <treeId>' ou crie uma nova.");
             return false;
         }

         if (activeRootNode.IsTemplate && action != "show") {
              Console.WriteLine("ERRO: Templates são somente leitura (exceto 'show'). Selecione uma árvore de cliente.");
              return false;
         }

        switch (action)
        {
            case "add-node": changed = AddNodeCommand(args, activeRootNode); break;
            case "add-alloc": changed = AddAllocationCommand(args, activeRootNode); break;
            case "deallocate": changed = DeallocateCommand(args, activeRootNode); break;
            case "update-limit": changed = UpdateLimitCommand(args, activeRootNode); break;
            case "set-overlimit-permission": changed = SetOverlimitPermissionCommand(args, activeRootNode); break;
            case "bulk-alloc": changed = BulkAllocateCommand(args, activeRootNode); break;
            case "bulk-dealloc": changed = BulkDeallocateCommand(args, activeRootNode); break;
            case "show": ShowTreeCommand(args, activeRootNode); break;
            default: Console.WriteLine($"Comando desconhecido ou inválido neste contexto: '{command}'. Digite 'ajuda'."); break;
        }
        return changed;
    }

     static void ListTemplatesCommand()
     {
         if (repository == null) return;
         Console.WriteLine("\n--- Modelos de Árvore Disponíveis ---");
         var templates = repository.ListTemplates().ToList();
         if (!templates.Any()) {
             Console.WriteLine("Nenhum modelo encontrado.");
             return;
         }
         foreach (var t in templates) { Console.WriteLine(t); }
     }

     static void ListCustomerTreesCommand()
     {
          if (repository == null) return;
         Console.WriteLine("\n--- Árvores de Cliente Existentes ---");
         var trees = repository.ListCustomerTrees().ToList();
          if (!trees.Any()) {
             Console.WriteLine("Nenhuma árvore de cliente encontrada.");
             return;
         }
         foreach (var t in trees) { Console.WriteLine(t); }
     }

      static void CreateTemplateCommand() {
          Console.WriteLine("\n--- Criar Novo Modelo de Árvore ---");
           Console.Write("Nome do modelo (nó raiz): "); string? name = Console.ReadLine();
           Console.Write("Tipo do nó raiz: "); string? type = Console.ReadLine();
           Console.Write("Limite de crédito (informativo, pode ser 0): "); string? limitStr = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(type) || !decimal.TryParse(limitStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal limit) || limit < 0) {
                Console.WriteLine("Erro: Dados inválidos para o modelo."); return;
            }

            try {
                 var templateRoot = new CreditNode(name, type, limit);
                 templateRoot.TreeId = Guid.NewGuid();
                 templateRoot.IsTemplate = true;
                 templateRoot.CreatedAtDate = DateTime.Now;

                 repository?.SaveTree(templateRoot);
                 Console.WriteLine($"Modelo '{name}' (ID: {templateRoot.TreeId}) criado com sucesso.");
            } catch (Exception ex) {
                 Console.WriteLine($"Erro ao criar ou salvar modelo: {ex.Message}");
            }
      }

      static void CreateTreeCommand(string[] args) {
            if (repository == null) return;
            if (args.Length != 2) {
                 Console.WriteLine("Uso: create-tree <templateId> <customerId>");
                 return;
            }
            if (!Guid.TryParse(args[0], out Guid templateId)) {
                 Console.WriteLine("Erro: ID do template inválido."); return;
            }
            string customerId = args[1];
             if (string.IsNullOrWhiteSpace(customerId)) {
                  Console.WriteLine("Erro: ID do Cliente não pode ser vazio."); return;
             }

            try {
                 CreditNode? newTree = repository.CreateTreeFromTemplate(templateId, customerId);
                 if (newTree != null) {
                     Console.WriteLine($"Árvore para o cliente '{customerId}' (ID: {newTree.TreeId}) criada com sucesso a partir do template '{templateId}'.");
                     activeRootNode = newTree;
                     Console.WriteLine($"Árvore '{newTree.Name}' selecionada.");
                 }
            } catch (Exception ex) {
                 Console.WriteLine($"Erro ao criar árvore do template: {ex.Message}");
            }
      }

      static void SelectTreeCommand(string[] args) {
          if (repository == null) return;
          if (args.Length != 1) {
              Console.WriteLine("Uso: select-tree <treeId>");
              return;
          }
          if (!Guid.TryParse(args[0], out Guid treeId)) {
              Console.WriteLine("Erro: Tree ID inválido."); return;
          }

          try {
               CreditNode? tree = repository.LoadTree(treeId);
               if (tree != null) {
                   activeRootNode = tree;
                   Console.WriteLine($"Árvore '{activeRootNode.Name}' (ID: {activeRootNode.TreeId}) selecionada.");
                    if (activeRootNode.IsTemplate) {
                         Console.WriteLine("AVISO: Você selecionou um template (somente leitura).");
                    }
               } else {
                    Console.WriteLine($"Árvore com ID {treeId} não encontrada.");
               }
          } catch (Exception ex) {
                Console.WriteLine($"Erro ao carregar árvore {treeId}: {ex.Message}");
          }
      }

    static bool AddNodeCommand(string[] args, CreditNode currentActiveRoot)
    {
        if (args.Length < 4 || args.Length > 5) throw new ArgumentException("Uso: add-node <pai> <nome> <tipo> <limite> [permite_overlimit(true|false)]");
        string parentName = args[0];
        string newNodeName = args[1];
        string newNodeType = args[2];
        decimal newNodeLimit = ParseNonNegativeDecimalOrThrow(args[3], "limite");
        bool allowOverLimit = false;
        if (args.Length == 5) { if (!bool.TryParse(args[4], out allowOverLimit)) { throw new ArgumentException("Valor inválido para 'permite_overlimit'. Use 'true' or 'false'."); } }

        CreditNode parentNode = FindNodeOrThrow(parentName, currentActiveRoot);
        var newNode = new CreditNode(newNodeName, newNodeType, newNodeLimit, parent: parentNode, allowOverLimitUpdate: allowOverLimit);
        parentNode.AddChild(newNode);
        Console.WriteLine($"Nó '{newNodeName}' adicionado.");
        return true;
    }

    static bool AddAllocationCommand(string[] args, CreditNode currentActiveRoot) {
         if (args.Length != 2) throw new ArgumentException("Uso: add-alloc <nome_folha> <valor>");
          string leafNodeName = args[0]; decimal allocationValue = ParsePositiveDecimalOrThrow(args[1], "valor");
          CreditNode leafNode = FindNodeOrThrow(leafNodeName, currentActiveRoot);
          var adjustment = new CreditAdjustment(allocationValue);
          leafNode.AddAdjustment(adjustment);
          Console.WriteLine($"Alocação de {allocationValue:C} adicionada a '{leafNodeName}'.");
          return true;
     }
     static bool DeallocateCommand(string[] args, CreditNode currentActiveRoot) {
          if (args.Length != 2) throw new ArgumentException("Uso: deallocate <nome_folha> <valor>");
           string leafNodeName = args[0]; decimal valueToRemove = ParsePositiveDecimalOrThrow(args[1], "valor");
           CreditNode leafNode = FindNodeOrThrow(leafNodeName, currentActiveRoot);
           var adjustment = new CreditAdjustment(-valueToRemove);
           leafNode.AddAdjustment(adjustment);
           Console.WriteLine($"Desalocação de {valueToRemove:C} registrada para '{leafNodeName}'.");
          return true;
     }
     static bool UpdateLimitCommand(string[] args, CreditNode currentActiveRoot) {
           if (args.Length != 2) throw new ArgumentException("Uso: update-limit <nome_no> <novo_limite>");
            string nodeName = args[0]; decimal newLimit = ParseNonNegativeDecimalOrThrow(args[1], "novo_limite");
            CreditNode node = FindNodeOrThrow(nodeName, currentActiveRoot);
            node.UpdateLimit(newLimit);
            Console.WriteLine($"Limite de '{nodeName}' atualizado para {newLimit:C}.");
            if(node.CurrentOverLimit > 0) {Console.ForegroundColor = ConsoleColor.Yellow; Console.WriteLine($"ATENÇÃO: Nó '{nodeName}' com over limit registrado de {node.CurrentOverLimit:C}."); Console.ResetColor();}
            else if (node.CreditTaken > node.CreditLimit && node.AllowOverLimitUpdate) {Console.ForegroundColor = ConsoleColor.Yellow; Console.WriteLine($"ATENÇÃO: Nó '{nodeName}' permanece em over limit."); Console.ResetColor();}
          return true;
      }
       static bool SetOverlimitPermissionCommand(string[] args, CreditNode currentActiveRoot) {
            if (args.Length != 2) throw new ArgumentException("Uso: set-overlimit-permission <nome_no> <true|false>");
            string nodeName = args[0]; bool allow; if (!bool.TryParse(args[1], out allow)) { throw new ArgumentException("Use 'true' or 'false'."); }
            CreditNode node = FindNodeOrThrow(nodeName, currentActiveRoot);
            node.SetAllowOverLimit(allow);
           return true;
       }
        static bool BulkAllocateCommand(string[] args, CreditNode currentActiveRoot) {
            if (args.Length != 1) throw new ArgumentException("Uso: bulk-alloc <caminho_arquivo_csv>");
            return ProcessBulkFile(args[0], AdjustmentType.Allocation, currentActiveRoot);
        }
        static bool BulkDeallocateCommand(string[] args, CreditNode currentActiveRoot) {
             if (args.Length != 1) throw new ArgumentException("Uso: bulk-dealloc <caminho_arquivo_csv>");
             return ProcessBulkFile(args[0], AdjustmentType.Deallocation, currentActiveRoot);
        }
        static bool ProcessBulkFile(string filePath, AdjustmentType type, CreditNode currentActiveRoot) {
             if (!File.Exists(filePath)) throw new FileNotFoundException($"Arquivo não encontrado: {filePath}");
             Console.WriteLine($"\nProcessando arquivo '{filePath}' para {type}...");
             int successCount = 0; int failCount = 0;
             var lines = File.ReadAllLines(filePath);
             bool anyChange = false;
             for (int i = 0; i < lines.Length; i++) {
                 string line = lines[i].Trim(); if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue; string[] parts = line.Split(',');
                 if (parts.Length != 2) { Console.ForegroundColor = ConsoleColor.Yellow; Console.WriteLine($"Linha {i + 1} ignorada: formato inválido -> '{line}'"); Console.ResetColor(); failCount++; continue; }
                 string nodeName = parts[0].Trim(); string valueStr = parts[1].Trim();
                 try {
                     decimal value = ParsePositiveDecimalOrThrow(valueStr, $"Valor linha {i + 1}");
                     decimal adjustmentValue = (type == AdjustmentType.Allocation) ? value : -value;
                     CreditNode node = FindNodeOrThrow(nodeName, currentActiveRoot);
                     var adjustment = new CreditAdjustment(adjustmentValue);
                     node.AddAdjustment(adjustment);
                     Console.ForegroundColor = ConsoleColor.Green; Console.WriteLine($"Linha {i + 1}: {type} de {value:C} para '{nodeName}' - SUCESSO"); Console.ResetColor();
                     successCount++; anyChange = true;
                 } catch (Exception ex) { Console.ForegroundColor = ConsoleColor.Red; Console.WriteLine($"Linha {i + 1}: FALHA ao processar '{line}' - {ex.Message}"); Console.ResetColor(); failCount++; }
             }
             Console.WriteLine($"\nProcessamento concluído: {successCount} sucesso(s), {failCount} falha(s).");
             return anyChange;
        }

    static void ShowTreeCommand(string[] args, CreditNode currentActiveRoot) {
        Console.WriteLine("\n--- Exibindo Árvore ---");
        if (args.Length == 0) { currentActiveRoot.DisplayNodeInfo(); }
        else if (args.Length == 1) {
             try {
                 CreditNode startNode = FindNodeOrThrow(args[0], currentActiveRoot);
                 startNode.DisplayNodeInfo();
             } catch (KeyNotFoundException ex) { Console.WriteLine(ex.Message); }
        } else { throw new ArgumentException("Uso: show [nome_no]"); }
    }

     static CreditNode FindNodeOrThrow(string nodeName, CreditNode currentActiveRoot)
     {
         var node = currentActiveRoot.FindNode(nodeName);
         if (node == null) throw new KeyNotFoundException($"Nó '{nodeName}' não encontrado na árvore ativa '{currentActiveRoot.Name}'.");
         return node;
     }

     static decimal ParsePositiveDecimalOrThrow(string valueStr, string argumentName) {
          if (!decimal.TryParse(valueStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal value) || value <= 0)
          { throw new ArgumentException($"Valor inválido para '{argumentName}'. Deve ser número positivo.", argumentName); }
          return value;
     }
     static decimal ParseNonNegativeDecimalOrThrow(string valueStr, string argumentName) {
         if (!decimal.TryParse(valueStr, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal value) || value < 0)
         { throw new ArgumentException($"Valor inválido para '{argumentName}'. Deve ser número não negativo.", argumentName); }
         return value;
     }
     static void ShowHelp() {
         Console.WriteLine("\n--- Comandos Gerais ---");
         Console.WriteLine(" ajuda                      - Mostra esta ajuda.");
         Console.WriteLine(" list-templates             - Lista os modelos de árvore disponíveis.");
         Console.WriteLine(" list-trees                 - Lista as árvores de cliente existentes.");
         Console.WriteLine(" create-template            - Inicia a criação de um novo modelo de árvore.");
         Console.WriteLine(" create-tree <templateId> <clienteId> - Cria árvore para cliente a partir de um modelo.");
         Console.WriteLine(" select-tree <treeId>       - Seleciona uma árvore (cliente ou template) para operar.");
         Console.WriteLine(" sair                       - Encerra a aplicação.");
         Console.WriteLine("\n--- Comandos da Árvore Ativa (Requer seleção) ---");
         Console.WriteLine(" show [nome_no]             - Exibe a árvore ativa ou sub-árvore.");
         Console.WriteLine(" add-node <pai> <nome> <tipo> <limite> [permite_overlimit]");
         Console.WriteLine("                            - Adiciona nó filho na árvore ativa.");
         Console.WriteLine(" set-overlimit-permission <nome> <true|false>");
         Console.WriteLine("                            - Define permissão de over limit no nó ativo.");
         Console.WriteLine(" add-alloc <folha> <valor>  - Adiciona ALOCAÇÃO na árvore ativa.");
         Console.WriteLine(" deallocate <folha> <valor> - Adiciona DESALOCAÇÃO na árvore ativa.");
         Console.WriteLine(" update-limit <nome> <novo_limite>");
         Console.WriteLine("                            - Atualiza limite de nó na árvore ativa.");
         Console.WriteLine(" bulk-alloc <arquivo.csv>   - Adiciona alocações em massa na árvore ativa.");
         Console.WriteLine(" bulk-dealloc <arquivo.csv> - Adiciona desalocações em massa na árvore ativa.");
    }
}