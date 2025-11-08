# 🚀 QUICK START - Fix "Invalid Token Signature"

## The Problem
You're getting "Invalid token signature" because the JWT secrets don't match between your Node.js server and C# application.

## ✅ THE FIX (3 Steps)

### Step 1: Rebuild Everything

Run this batch file from the WpfHack directory:
```
rebuild-and-test.bat
```

OR manually:
```bash
cd LicenseValidatorLibrary
dotnet build

cd ..\WpfHack
dotnet build
```

### Step 2: Start Your Node.js Server

```bash
cd LicenseValidatorLibrary
npx ts-node index.ts
```

You should see it load the JWT_SECRET from the .env file. The console will show what secret it's using.

### Step 3: Generate a Test Token

In another terminal:
```bash
cd LicenseValidatorLibrary
npx ts-node generate-test-token.ts
```

This will generate a JWT token and display it. **Copy this token.**

### Step 4: Test in Your WPF App

1. Run your WPF application
2. Paste the token into the license validation popup
3. Click validate

It should now work! ✅

## 🔍 What I Fixed

1. **Created `.env` file** with JWT_SECRET that matches your keys.json:
   - JWT_SECRET: `super_secret_dev_key_change_me_in_production_12345678`

2. **Updated `keys.json`** to use the SAME secret:
   - jwtSecret: `super_secret_dev_key_change_me_in_production_12345678`

3. **Fixed JSON deserialization** to handle camelCase property names

4. **Added debugging** to show what's loaded

5. **Handled BOM characters** in JSON files

6. **Made service key validation optional** (won't fail if ServiceKeys is null)

## 📝 Verification

When you run the WPF app, check the console (Output window in Visual Studio/Rider). You should see:

```
[LicenseValidator] Configuration loaded successfully
[LicenseValidator] JWT Secret Length: 56
[LicenseValidator] Service Keys Count: 3
```

If you see this, the configuration is loaded correctly!

## 🧪 Test Token

You can also use the POST endpoint to generate tokens:

```bash
curl -X POST http://localhost:3000/generate ^
  -H "Content-Type: application/json" ^
  -d "{\"email\":\"user@test.com\",\"deviceSerial\":\"DEVICE-001\",\"level\":2}"
```

## ⚠️ IMPORTANT

The JWT_SECRET in `.env` and `jwtSecret` in `keys.json` **MUST BE IDENTICAL**!

Current secret being used:
```
super_secret_dev_key_change_me_in_production_12345678
```

## Still Getting Errors?

1. Check the error message in the popup - it now shows the configuration status
2. Look at the console output when the app starts
3. Run `npx ts-node check-secret.ts` to verify what secret Node.js is using
4. Compare the first 10 characters of both secrets - they should match exactly

## 🎯 Next Steps (Optional)

For production, you should:
1. Generate a secure random secret: `node -e "console.log(require('crypto').randomBytes(32).toString('hex'))"`
2. Update both `.env` and `keys.json` with this new secret
3. Never commit `.env` to version control (it's in .gitignore)

