import * as fs from 'fs';
import * as path from 'path';

interface ParsedEnum {
  name: string;
  values: Array<{ name: string; value?: string }>;
}

interface ParsedStruct {
  name: string;
  fields: Array<{ type: string; name: string }>;
}

interface ParsedInterface {
  name: string;
  extends: string[];
  properties: Array<{ name: string; type: string; readonly: boolean }>;
  methods: Array<{ name: string; params: Array<{ name: string; type: string }>; returnType: string }>;
  events: Array<{ name: string; type: string }>;
}

function mapWinRTTypeToTypeScript(winrtType: string): string {
  const typeMap: Record<string, string> = {
    'String': 'string',
    'Boolean': 'boolean',
    'Int32': 'number',
    'UInt8': 'number',
    'UInt32': 'number',
    'IInspectable': 'unknown',
    'Object': 'unknown',
    'Uri': 'string',
    'IAsyncAction': 'Promise<void>',
    'Windows.Foundation.IAsyncAction': 'Promise<void>',
  };

  // Handle arrays
  if (winrtType.endsWith('[]')) {
    const baseType = winrtType.slice(0, -2);
    return `${mapWinRTTypeToTypeScript(baseType)}[]`;
  }

  // Handle IMap<K,V>
  const mapMatch = winrtType.match(/IMap<(.+),\s*(.+)>/);
  if (mapMatch) {
    const keyType = mapWinRTTypeToTypeScript(mapMatch[1].trim());
    const valueType = mapWinRTTypeToTypeScript(mapMatch[2].trim());
    return `Record<${keyType}, ${valueType}>`;
  }

  // Handle Windows.Storage.Streams.IRandomAccessStreamReference
  if (winrtType.includes('IRandomAccessStreamReference')) {
    return 'string';
  }

  // Handle Windows.System.VirtualKeyModifiers
  if (winrtType.includes('VirtualKeyModifiers')) {
    return 'number';
  }

  // Handle Windows.Foundation.IClosable
  if (winrtType.includes('Windows.Foundation.IClosable')) {
    return 'IClosable';
  }

  // Remove Microsoft.CommandPalette.Extensions namespace prefix
  const stripped = winrtType.replace(/^Microsoft\.CommandPalette\.Extensions\./, '');

  return typeMap[stripped] || stripped;
}

function parseEnums(content: string): ParsedEnum[] {
  const enums: ParsedEnum[] = [];
  
  // Remove single-line comments first
  const cleanContent = content.replace(/\/\/.*$/gm, '');
  const enumRegex = /enum\s+(\w+)\s*\{([^}]+)\}/g;
  
  let match;
  while ((match = enumRegex.exec(cleanContent)) !== null) {
    const name = match[1];
    const valuesStr = match[2];
    const values = valuesStr.split(',').map(v => {
      const trimmed = v.trim();
      if (!trimmed) return null;
      const parts = trimmed.split('=');
      return {
        name: parts[0].trim(),
        value: parts[1]?.trim()
      };
    }).filter(v => v && v.name) as Array<{ name: string; value?: string }>;
    
    enums.push({ name, values });
  }
  
  return enums;
}

function parseStructs(content: string): ParsedStruct[] {
  const structs: ParsedStruct[] = [];
  
  // Remove comments first
  const cleanContent = content.replace(/\/\/.*$/gm, '');
  const structRegex = /struct\s+(\w+)\s*\{([^}]+)\}/g;
  
  let match;
  while ((match = structRegex.exec(cleanContent)) !== null) {
    const name = match[1];
    const fieldsStr = match[2];
    const fields = fieldsStr.split(';').map(f => {
      const trimmed = f.trim();
      if (!trimmed) return null;
      const parts = trimmed.split(/\s+/);
      if (parts.length >= 2) {
        const type = parts.slice(0, -1).join(' ');
        const fieldName = parts[parts.length - 1];
        return { type: mapWinRTTypeToTypeScript(type), name: fieldName };
      }
      return null;
    }).filter(f => f !== null) as Array<{ type: string; name: string }>;
    
    structs.push({ name, fields });
  }
  
  return structs;
}

function parseInterfaces(content: string): ParsedInterface[] {
  const interfaces: ParsedInterface[] = [];
  
  // Remove comments and attributes
  let cleanContent = content.replace(/\/\/.*$/gm, '');
  cleanContent = cleanContent.replace(/\[uuid\([^\]]+\)\]/g, '');
  cleanContent = cleanContent.replace(/\[contract\([^\]]+\)\]/g, '');
  cleanContent = cleanContent.replace(/\[contractversion\([^\]]+\)\]/g, '');
  
  const lines = cleanContent.split('\n');
  let i = 0;
  
  while (i < lines.length) {
    let line = lines[i].trim();
    
    // Skip empty lines, namespace, and apicontract
    if (!line || line.includes('namespace') || line.includes('apicontract')) {
      i++;
      continue;
    }
    
    // Look for interface declaration
    if (line.includes('interface ')) {
      // Collect the full declaration until we find an opening brace
      let declaration = line;
      let lookAhead = i + 1;
      
      while (!declaration.includes('{') && lookAhead < lines.length) {
        declaration += ' ' + lines[lookAhead].trim();
        lookAhead++;
      }
      
      // Extract interface name and requires clause
      const interfaceMatch = declaration.match(/interface\s+(\w+)(?:\s+requires\s+([^{]+))?\s*\{/);
      if (interfaceMatch) {
        const name = interfaceMatch[1];
        const requiresStr = interfaceMatch[2]?.trim() || '';
        const extendsList = requiresStr
          ? requiresStr.split(',').map(e => mapWinRTTypeToTypeScript(e.trim())).filter(e => e)
          : [];
        
        const properties: Array<{ name: string; type: string; readonly: boolean }> = [];
        const methods: Array<{ name: string; params: Array<{ name: string; type: string }>; returnType: string }> = [];
        const events: Array<{ name: string; type: string }> = [];
        
        // Check if body is empty (interface ends with {} or { })
        const afterBraceMatch = declaration.match(/\{\s*\}/);
        if (afterBraceMatch) {
          interfaces.push({
            name,
            extends: extendsList,
            properties,
            methods,
            events
          });
          i = lookAhead;
          continue;
        }
        
        // Parse body until we find closing brace
        i = lookAhead;
        let bodyContent = '';
        let braceCount = 1;
        
        // Check if there's content after { on declaration line
        const splitAtBrace = declaration.split('{');
        if (splitAtBrace.length > 1 && splitAtBrace[1].trim()) {
          bodyContent += splitAtBrace[1] + ' ';
        }
        
        // Continue collecting body lines
        while (i < lines.length && braceCount > 0) {
          const bodyLine = lines[i];
          
          // Count braces
          for (const char of bodyLine) {
            if (char === '{') braceCount++;
            if (char === '}') {
              braceCount--;
              if (braceCount === 0) break;
            }
          }
          
          if (braceCount === 0) {
            // Add content before the closing brace
            const beforeClosing = bodyLine.substring(0, bodyLine.lastIndexOf('}'));
            if (beforeClosing.trim()) {
              bodyContent += beforeClosing + ' ';
            }
            break;
          }
          
          bodyContent += bodyLine + ' ';
          i++;
        }
        
        // Parse the collected body content
        const statements = bodyContent.split(';').map(s => s.trim()).filter(s => s);
        
        for (const stmt of statements) {
          // Event
          if (stmt.includes('event')) {
            const eventMatch = stmt.match(/event\s+.+\s+(\w+)\s*$/);
            if (eventMatch) {
              events.push({ name: eventMatch[1], type: 'EventHandler' });
            }
            continue;
          }
          
          // Method with return type and params
          const methodMatch = stmt.match(/^(.+?)\s+(\w+)\s*\(([^)]*)\)\s*$/);
          if (methodMatch) {
            const returnType = mapWinRTTypeToTypeScript(methodMatch[1].trim());
            const methodName = methodMatch[2];
            const paramsStr = methodMatch[3].trim();
            
            const params = paramsStr ? paramsStr.split(',').map(p => {
              const trimmed = p.trim();
              const parts = trimmed.split(/\s+/);
              if (parts.length >= 2) {
                const paramType = mapWinRTTypeToTypeScript(parts.slice(0, -1).join(' '));
                const paramName = parts[parts.length - 1];
                return { name: paramName, type: paramType };
              }
              return null;
            }).filter(p => p !== null) as Array<{ name: string; type: string }> : [];
            
            methods.push({ name: methodName, params, returnType });
            continue;
          }
          
          // Property with getter/setter
          const propMatch = stmt.match(/^(.+?)\s+(\w+)\s*\{\s*(get;?\s*set?|get|set)\s*\}\s*$/);
          if (propMatch) {
            const propType = mapWinRTTypeToTypeScript(propMatch[1].trim());
            const propName = propMatch[2];
            const accessors = propMatch[3];
            const readonly = !accessors.includes('set');
            
            properties.push({ name: propName, type: propType, readonly });
            continue;
          }
        }
        
        interfaces.push({
          name,
          extends: extendsList,
          properties,
          methods,
          events
        });
      }
    }
    
    i++;
  }
  
  return interfaces;
}

function generateTypeScript(enums: ParsedEnum[], structs: ParsedStruct[], interfaces: ParsedInterface[]): string {
  let output = '// Auto-generated from Microsoft.CommandPalette.Extensions.idl\n';
  output += '// DO NOT EDIT MANUALLY\n\n';
  
  // Generate enums
  for (const enumDef of enums) {
    output += `export enum ${enumDef.name} {\n`;
    for (const value of enumDef.values) {
      if (value.value !== undefined) {
        output += `  ${value.name} = ${value.value},\n`;
      } else {
        output += `  ${value.name},\n`;
      }
    }
    output += '}\n\n';
  }
  
  // Generate structs as interfaces
  for (const struct of structs) {
    output += `export interface ${struct.name} {\n`;
    for (const field of struct.fields) {
      output += `  ${field.name}: ${field.type};\n`;
    }
    output += '}\n\n';
  }
  
  // Generate interfaces
  for (const iface of interfaces) {
    const extendsClause = iface.extends.length > 0 
      ? ` extends ${iface.extends.join(', ')}`
      : '';
    
    output += `export interface ${iface.name}${extendsClause} {\n`;
    
    // Properties
    for (const prop of iface.properties) {
      const readonlyModifier = prop.readonly ? 'readonly ' : '';
      output += `  ${readonlyModifier}${prop.name}: ${prop.type};\n`;
    }
    
    // Events as callback properties
    for (const event of iface.events) {
      output += `  ${event.name}?: (args: unknown) => void;\n`;
    }
    
    // Methods
    for (const method of iface.methods) {
      const params = method.params.map(p => `${p.name}: ${p.type}`).join(', ');
      output += `  ${method.name}(${params}): ${method.returnType};\n`;
    }
    
    output += '}\n\n';
  }
  
  // Add IClosable interface
  output += `export interface IClosable {\n`;
  output += `  close(): void;\n`;
  output += `}\n\n`;
  
  return output;
}

function main() {
  const idlPath = path.join(__dirname, '../../Microsoft.CommandPalette.Extensions/Microsoft.CommandPalette.Extensions.idl');
  const outputPath = path.join(__dirname, '../src/generated/types.ts');
  
  console.log('Reading IDL file from:', idlPath);
  const content = fs.readFileSync(idlPath, 'utf-8');
  
  console.log('Parsing IDL...');
  const enums = parseEnums(content);
  const structs = parseStructs(content);
  const interfaces = parseInterfaces(content);
  
  console.log(`Found ${enums.length} enums, ${structs.length} structs, ${interfaces.length} interfaces`);
  
  console.log('Generating TypeScript...');
  const typescript = generateTypeScript(enums, structs, interfaces);
  
  // Ensure output directory exists
  const outputDir = path.dirname(outputPath);
  if (!fs.existsSync(outputDir)) {
    fs.mkdirSync(outputDir, { recursive: true });
  }
  
  fs.writeFileSync(outputPath, typescript, 'utf-8');
  console.log('Generated types at:', outputPath);
}

main();
