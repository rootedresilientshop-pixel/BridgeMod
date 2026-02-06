# GitHub Release Checklist

Everything is prepared for you to push BridgeMod to GitHub. Follow these steps:

## Before Creating the Repository

- [ ] Review the tone and messaging in [README.md](README.md) - does it feel right?
- [ ] Check that [CONSTITUTION.md](CONSTITUTION.md) represents your vision
- [ ] Verify you're happy with the license choice (MIT)
- [ ] Decide on a GitHub username/organization (for URLs in metadata)

## Step 1: Create the GitHub Repository

1. Go to [github.com/new](https://github.com/new)
2. Create a new repository named `BridgeMod` (or your preferred name)
3. Choose public (we want developers to discover this)
4. **Do NOT initialize with README, license, or gitignore** (we already have them)
5. Click "Create repository"

## Step 2: Update Repository URLs

The following files contain placeholder URLs that need updating:

- [ ] **sdk/BridgeMod.SDK.csproj** - Update these lines:
  ```xml
  <PackageProjectUrl>https://github.com/yourusername/BridgeMod</PackageProjectUrl>
  <RepositoryUrl>https://github.com/yourusername/BridgeMod</RepositoryUrl>
  ```

- [ ] **.github/ISSUE_TEMPLATE/feature_request.md** - Review the Constitution reference paths

## Step 3: Push to GitHub

```bash
cd C:\Users\gardn\BridgeMod

# The repo is already initialized (from Phase 1 setup)
# Just add the remote and push:

git remote add origin https://github.com/yourusername/BridgeMod.git
git branch -M main                    # Rename master to main (GitHub convention)
git push -u origin main

# Verify: GitHub should now show your code
```

## Step 4: Verify on GitHub

After pushing:

- [ ] Repository is public and discoverable
- [ ] README.md displays correctly
- [ ] All files are present (check file browser)
- [ ] LICENSE shows in repo metadata
- [ ] GitHub recognizes the license (MIT badge)

## Step 5: Configure GitHub Settings

In your repository settings:

1. **General**
   - [ ] Description: "Safe, transparent, data-driven modding platform for developers and modders"
   - [ ] Website: `https://github.com/yourusername/BridgeMod` (or your website if you have one)
   - [ ] Topics: `modding`, `game-development`, `validation`, `sandbox`

2. **Branches**
   - [ ] Set `main` as default branch
   - [ ] (Optional) Enable branch protection for `main`:
     - Require pull request reviews before merging
     - Require status checks to pass

3. **Pages** (optional - to host docs)
   - [ ] Enable GitHub Pages
   - [ ] Source: Deploy from a branch
   - [ ] Branch: `main` / root folder

## Step 6: Test GitHub Actions

- [ ] Push a test commit
- [ ] Verify `.github/workflows/build.yml` runs automatically
- [ ] Check that tests pass

## Step 7: Create First Release

```bash
# Create an annotated tag
git tag -a v0.1.0 -m "Phase 1: Foundations - Initial Release"
git push origin v0.1.0
```

Then on GitHub:

1. Go to Releases
2. Click "Draft a new release"
3. Select tag `v0.1.0`
4. Title: "v0.1.0 - Phase 1: Foundations"
5. Description: Copy content from [CHANGELOG.md](CHANGELOG.md)
6. Publish

## Step 8: (Optional) Publish to NuGet

When ready to share the NuGet package:

```bash
# Build the package locally first
cd sdk
dotnet pack --configuration Release

# Create account at https://www.nuget.org if needed
# Then push:
dotnet nuget push bin/Release/BridgeMod.SDK.0.1.0.nupkg \
  -k <your-nuget-api-key> \
  -s https://api.nuget.org/v3/index.json
```

## Step 9: Share with Community

After releasing:

- [ ] Post on Twitter/X with GitHub link and a brief note about the vision
- [ ] Share in relevant game dev communities (Reddit, Discord, etc.)
- [ ] (Optional) Write a blog post about the project and philosophy

## Checklist Summary

```
GitHub Setup:
  ☐ Create GitHub repository
  ☐ Update repository URLs in csproj
  ☐ Push to main branch
  ☐ Verify files on GitHub

GitHub Configuration:
  ☐ Set description and topics
  ☐ Enable GitHub Pages (optional)
  ☐ Configure branch protection (optional)

Releases:
  ☐ Test GitHub Actions
  ☐ Create v0.1.0 release tag
  ☐ Publish release on GitHub
  ☐ (Optional) Publish to NuGet

Community:
  ☐ Share on social media
  ☐ Post in game dev communities
```

## What You Have Ready

✅ **Project Structure** - Clean, organized, follows conventions

✅ **Documentation** - Comprehensive and welcoming

✅ **Governance** - CONSTITUTION.md defines principles

✅ **Community** - CODE_OF_CONDUCT.md, CONTRIBUTING.md, issue templates

✅ **CI/CD** - GitHub Actions workflows ready

✅ **Testing** - 30+ tests, ready to run

✅ **NuGet Metadata** - Package info ready for publishing

✅ **License** - MIT, clear and permissive

## Notes

- All files reference each other correctly
- The tone throughout is confident but humble
- Vision is clear in README and CONSTITUTION
- Community is welcomed in CONTRIBUTING
- Change log documents what's been built

**The project is ready. You're bridging the gap. Let's show the world.**

---

Questions? Check [CONTRIBUTING.md](CONTRIBUTING.md) or open an issue on GitHub after you push.
