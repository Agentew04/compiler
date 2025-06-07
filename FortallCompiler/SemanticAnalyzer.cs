using FortallCompiler.Ast;
using Type = FortallCompiler.Ast.Type;

namespace FortallCompiler;

public class SemanticAnalyzer {

    private ScopeData globalScope = new();
    
    private readonly List<Diagnostic> diagnostics = [];
    
    public (bool success, List<Diagnostic> diagnostics) Analyze(ProgramNode ast) {

        // first step: verificar se o programa tem uma main
        bool hasMain = ast.TopLevelNodes
            .Where(x => x is FunctionNode)
            .Cast<FunctionNode>()
            .Any(x => x.Name.Equals("main", StringComparison.CurrentCultureIgnoreCase));

        if (!hasMain) {
            diagnostics.Add(new Diagnostic("O programa deve conter uma função 'main' definida.", 0,0));
        }
        
        // indexar nome das funcoes
        ast.TopLevelNodes
            .Where(x => x is FunctionNode)
            .Cast<FunctionNode>()
            .ForEach(x => {
                if (globalScope.AvailableFunctionNames.Contains(x.Name)) {
                    diagnostics.Add(new Diagnostic($"A função '{x.Name}' já foi declarada.", x.LineNumber, x.ColumnNumber));
                    return;
                }
                globalScope.AvailableFunctionNames.Add(x.Name);
                globalScope.Functions.Add(x.Name,x);
            });
        
        // indexar nome das variaveis globais
        ast.TopLevelNodes
            .Where(x => x is FieldDeclarationNode)
            .Cast<FieldDeclarationNode>()
            .ForEach(x => {
                if (globalScope.AvailableVariableNames.Contains(x.FieldName)) {
                    diagnostics.Add(new Diagnostic($"A variável global '{x.FieldName}' já foi declarada.", x.LineNumber, x.ColumnNumber));
                    return;
                }
                globalScope.AvailableVariableNames.Add(x.FieldName);
                globalScope.Variables.Add(x.FieldName, new VariableData(){FieldDeclaration = x});
            });
        
        // verificar conflitos de nomes globais vs funcoes
        foreach (KeyValuePair<string, VariableData> glob in globalScope.Variables) {
            FieldDeclarationNode variable = glob.Value.FieldDeclaration!;
            if (globalScope.AvailableFunctionNames.Contains(variable.FieldName)) {
                diagnostics.Add(new Diagnostic($"O nome '{glob}' está sendo usado tanto como variável global quanto como função.", variable.LineNumber, variable.ColumnNumber));
            }
        }
        
        // verificar se cada uma das variaveis globais com valor estao com valor correto
        foreach (string glob in globalScope.AvailableVariableNames) {
            FieldDeclarationNode fieldNode = globalScope.Variables[glob].FieldDeclaration!;
            if (fieldNode.InitValue is null) {
                continue;
            }
            // verificar se o tipo do valor inicial é compatível com o tipo da variável
            if (fieldNode.FieldType != fieldNode.InitValue.Type) {
                diagnostics.Add(new Diagnostic($"O valor inicial da variável global '{glob}' não é do tipo '{fieldNode.FieldType}'.", fieldNode.LineNumber, fieldNode.ColumnNumber));
            }
        }
        
        // agora parte para as verificacoes de cada funcao
        ast.TopLevelNodes
            .Where(x => x is FunctionNode)
            .Cast<FunctionNode>()
            .ForEach(CheckFunction);

        
        return (diagnostics.Count == 0, diagnostics);
    }
    
    private void CheckFunction(FunctionNode function) {
        ScopeData scope = new();
        scope.ParentScope = globalScope;
        
        foreach (StatementNode stmt in function.Body.Statements) {
            // verifica se a variavel declarada ja existe
            if (stmt is VariableDeclarationNode varDeclNode) {
                if (scope.VariableExists(varDeclNode.VariableName)) {
                    diagnostics.Add(new Diagnostic($"Variavel {varDeclNode.VariableName} ja esta declarada em outro lugar", varDeclNode.LineNumber, varDeclNode.ColumnNumber));
                }
                else {
                    // adicionar
                    scope.AvailableVariableNames.Add(varDeclNode.VariableName);
                    scope.Variables.Add(varDeclNode.VariableName, new VariableData(){VariableDeclarationNode = varDeclNode});
                }

                // se tem valor iniciado, verifica se condiz
                if (varDeclNode.InitValue is not null) {
                    if (varDeclNode.InitValue.ExpressionType == Type.Void) {
                        // tenta descobrir 
                        bool searchSuccess = TryGetExpressionType(varDeclNode.InitValue, scope);
                        if (!searchSuccess) {
                            Console.WriteLine("Erro brabo. Nao consegui deduzir o tipo de alguma expressao que estava como void :O");
                            // pula os outros checks pq algo maior deu errado
                            continue;
                        }
                    }
                    
                    if (varDeclNode.InitValue.ExpressionType != varDeclNode.VariableType) {
                        diagnostics.Add(new Diagnostic("Tipo gerado pela expressao nao bate com o tipo da variavel", varDeclNode.InitValue.LineNumber, varDeclNode.InitValue.ColumnNumber));
                    }
                }
                continue;
            }

            if (stmt is AssignmentNode assignmentNode) {
                if (!scope.VariableExists(assignmentNode.VariableName)) {
                    diagnostics.Add(new Diagnostic("A variavel nao foi declarada!", assignmentNode.LineNumber, assignmentNode.ColumnNumber));
                    continue;
                }
                // verifica se o tipo bate com o valor dado
                continue;
            }
        }
    }

    private bool TryGetExpressionType(ExpressionNode expr, ScopeData scope) {
        if (expr.ExpressionType != Type.Void) {
            return true;
        }

        if (expr is FunctionCallExpressionNode funcExpr) {
            string funcName = funcExpr.FunctionName;
            if (!scope.FunctionExists(funcName)) {
                return false;
            }
            FunctionNode func = scope.GetFunction(funcName)!;
            funcExpr.ExpressionType = func.ReturnType;
            return true;
        }

        if (expr is IdentifierExpressionNode idExpr) {
            string varName = idExpr.Name;
            if (!scope.VariableExists(varName)) {
                return false;
            }
            VariableData varData = scope.GetVariable(varName)!;
            Type t = varData.FieldDeclaration?.FieldType ?? varData.VariableDeclarationNode!.VariableType;
            idExpr.ExpressionType = t;
            return true;
        }
        
        return false;
    }
    
    public class ScopeData {
        public ScopeData? ParentScope { get; set; }

        public List<string> AvailableFunctionNames { get; set; } = [];
        public List<string> AvailableVariableNames { get; set; } = [];

        public Dictionary<string, FunctionNode> Functions { get; set; } = [];
        public Dictionary<string, VariableData> Variables { get; set; } = [];

        public bool VariableExists(string name) {
            if (AvailableVariableNames.Contains(name)) {
                return true;
            }
            return ParentScope is not null && ParentScope.VariableExists(name);
        }

        public VariableData? GetVariable(string name) {
            if (Variables.TryGetValue(name, out VariableData? data)) {
                return data;
            }

            VariableData? parentData = ParentScope?.GetVariable(name);
            return parentData;
        }

        public bool FunctionExists(string name) {
            if (AvailableFunctionNames.Contains(name)) {
                return true;
            }
            return ParentScope is not null && ParentScope.FunctionExists(name);
        }

        public FunctionNode? GetFunction(string name) {
            if (Functions.TryGetValue(name, out FunctionNode? data)) {
                return data;
            }

            FunctionNode? parentData = ParentScope?.GetFunction(name);
            return parentData;
        }
    }

    // classe intermediaria pra agrupar fields e variables
    public class VariableData {
        public FieldDeclarationNode? FieldDeclaration {get; set; }
        public VariableDeclarationNode? VariableDeclarationNode { get; set; }
    }
}