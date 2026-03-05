import * as fs from 'fs';
import * as path from 'path';
import * as readline from 'readline';

interface ExtensionConfig {
  name: string;
  displayName: string;
  description: string;
}

const rl = readline.createInterface({
  input: process.stdin,
  output: process.stdout,
});

function question(prompt: string): Promise<string> {
  return new Promise((resolve) => {
    rl.question(prompt, (answer) => {
      resolve(answer.trim());
    });
  });
}

async function getExtensionConfig(): Promise<ExtensionConfig> {
  // Check for command line arguments
  const args = process.argv.slice(2);
  
  if (args.length >= 3) {
    return {
      name: args[0],
      displayName: args[1],
      description: args[2],
    };
  }

  console.log('\n🎨 Command Palette Extension Scaffolding Tool\n');
  
  const name = await question('Extension name (kebab-case): ');
  if (!name) {
    throw new Error('Extension name is required');
  }

  const displayName = await question('Display name: ');
  if (!displayName) {
    throw new Error('Display name is required');
  }

  const description = await question('Description: ');
  if (!description) {
    throw new Error('Description is required');
  }

  return { name, displayName, description };
}

function validateName(name: string): boolean {
  return /^[a-z0-9\-_]+$/.test(name);
}

function createManifest(config: ExtensionConfig): object {
  return {
    $schema: 'https://www.powertoys.dev/cmdpal-extension/1.0.json',
    id: config.name,
    displayName: config.displayName,
    description: config.description,
    version: '0.1.0',
    author: 'PowerToys Extension Developer',
    minCmdPalVersion: '0.1.0',
  };
}

function createPackageJson(config: ExtensionConfig): object {
  return {
    name: `cmdpal-${config.name}`,
    version: '0.1.0',
    description: config.description,
    main: 'dist/index.js',
    types: 'dist/index.d.ts',
    scripts: {
      build: 'tsc',
      dev: 'tsc --watch',
      clean: 'rm -rf dist',
    },
    keywords: ['powertoys', 'command-palette', 'extension'],
    author: '',
    license: 'MIT',
    dependencies: {
      '@cmdpal/sdk': '^0.1.0',
    },
    devDependencies: {
      '@types/node': '^20.19.35',
      typescript: '^5.9.3',
    },
  };
}

function createTsconfigJson(): object {
  return {
    compilerOptions: {
      target: 'ES2020',
      module: 'commonjs',
      lib: ['ES2020'],
      outDir: './dist',
      rootDir: './src',
      strict: true,
      esModuleInterop: true,
      skipLibCheck: true,
      forceConsistentCasingInFileNames: true,
      resolveJsonModule: true,
      declaration: true,
      declarationMap: true,
      sourceMap: true,
    },
    include: ['src/**/*'],
    exclude: ['node_modules', 'dist'],
  };
}

function createHelloWorldTemplate(config: ExtensionConfig): string {
  const extensionName = config.displayName;
  const displayName = config.displayName;
  
  return `import { CommandProvider, CommandItem, InvokableCommand, CommandResult, ExtensionServer } from '@cmdpal/sdk';
import type { ICommandItem, ICommandResult } from '@cmdpal/sdk';

class HelloCommand extends InvokableCommand {
  constructor() {
    super({ name: 'Say Hello', id: 'hello' });
  }

  invoke(): ICommandResult {
    return CommandResult.showToast('Hello from ${extensionName}!');
  }
}

class MyProvider extends CommandProvider {
  get id() { return '${config.name}'; }
  get displayName() { return '${displayName}'; }
  get icon() { return undefined; }

  topLevelCommands(): ICommandItem[] {
    return [
      new CommandItem({
        command: new HelloCommand(),
        title: '${displayName}',
        subtitle: 'A Command Palette extension',
      }),
    ];
  }
}

ExtensionServer.register(new MyProvider());
ExtensionServer.start();
`;
}

function createGitignore(): string {
  return `# Dependencies
node_modules/
package-lock.json
yarn.lock

# Build output
dist/

# IDE
.vscode/
.idea/
*.swp
*.swo

# OS
.DS_Store
Thumbs.db

# Logs
*.log
npm-debug.log*
`;
}

async function createProject(config: ExtensionConfig): Promise<void> {
  if (!validateName(config.name)) {
    throw new Error('Extension name must contain only lowercase letters, numbers, hyphens, and underscores');
  }

  const projectDir = path.join(process.cwd(), config.name);

  // Check if directory already exists
  if (fs.existsSync(projectDir)) {
    throw new Error(`Directory '${config.name}' already exists`);
  }

  // Create project directory structure
  fs.mkdirSync(projectDir, { recursive: true });
  fs.mkdirSync(path.join(projectDir, 'src'), { recursive: true });

  // Write files
  fs.writeFileSync(
    path.join(projectDir, 'cmdpal.json'),
    JSON.stringify(createManifest(config), null, 2)
  );

  fs.writeFileSync(
    path.join(projectDir, 'package.json'),
    JSON.stringify(createPackageJson(config), null, 2)
  );

  fs.writeFileSync(
    path.join(projectDir, 'tsconfig.json'),
    JSON.stringify(createTsconfigJson(), null, 2)
  );

  fs.writeFileSync(
    path.join(projectDir, 'src', 'index.ts'),
    createHelloWorldTemplate(config)
  );

  fs.writeFileSync(
    path.join(projectDir, '.gitignore'),
    createGitignore()
  );

  console.log(`\n✅ Extension project created at '${projectDir}'\n`);
  console.log('Next steps:');
  console.log(`  1. cd ${config.name}`);
  console.log('  2. npm install');
  console.log('  3. npm run build');
  console.log('\nHappy coding! 🚀\n');
}

async function main(): Promise<void> {
  try {
    const config = await getExtensionConfig();
    rl.close();
    await createProject(config);
  } catch (error) {
    rl.close();
    console.error(`\n❌ Error: ${error instanceof Error ? error.message : String(error)}\n`);
    process.exit(1);
  }
}

main();
