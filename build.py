"""Build nuget package.
Before using this script, perform the following steps:

  * Update version in ThingsDB.csproj
  * Run (From the ThingsDB folder): dotnet pack
  * Run this script: python build.py <VERSION>
    Example python build.py 1.0.3
"""
import sys
import os
from xml.dom.minidom import parse, Document, Element
from subprocess import Popen, PIPE


PACKAGE_ID = 'ThingsDB.Connector'
TAGS = 'thingsdb database programming connector'
ICON = 'logo.png'
AUTHORS = 'joente'
CWD = os.path.abspath(os.path.curdir)
PROJECT_URL = 'https://github.com/thingsdb/ThingsDB-CSharp/'
REPOSITORY_URL = 'https://github.com/thingsdb/ThingsDB-CSharp.git'

def new_prettify(dom):
    return '\n'.join([
        line
        for line in dom.toprettyxml(indent=' '*2).split('\n')
        if line.strip()])


if __name__ == '__main__':
    if len(sys.argv) != 2:
        print('usage: python build.py <version>')
        sys.exit(1)

    version = sys.argv[1]
    filename = os.path.join(
        'ThingsDB',
        'obj',
        'Release',
        f'ThingsDB.{version}.nuspec')
    dom: Document = parse(filename)
    package: Element = dom.getElementsByTagName('package')[0]
    metadata: Element = package.getElementsByTagName('metadata')[0]
    files: Element = package.getElementsByTagName('files')[0]

    vers: Element = metadata.getElementsByTagName('version')[0]
    if vers != version:
        print(f'Verion in nuspec {vers} is not equal to {version}')
        sys.exit(1)

    try:
        package.removeAttribute('xmlns')
    except Exception as e:
        msg = str(e) or type(e).__name__
        print(f'{msg} (most likeley the xml is already build)')
        inp = input('\n Do you still want to run dotnet? (Y/N)')
        if inp.lower() != 'y':
            print('aborted')
            sys.exit(1)
    else:
        # Fix id
        id: Element = metadata.getElementsByTagName('id')[0]
        id.firstChild.nodeValue = PACKAGE_ID

        # Fix authors
        authors: Element = metadata.getElementsByTagName('authors')[0]
        authors.firstChild.nodeValue = AUTHORS

        # Remove licenseUrl
        licenseUrl: Element = metadata.getElementsByTagName('licenseUrl')[0]
        metadata.removeChild(licenseUrl)

        # Add icon to metadata
        icon: Element = dom.createElement('icon')
        icon.appendChild(dom.createTextNode(ICON))
        metadata.appendChild(icon)

        # Remove repository
        repository: Element = metadata.getElementsByTagName('repository')[0]
        metadata.removeChild(repository)

        # Add tags to metadata
        tags: Element = dom.createElement('tags')
        tags.appendChild(dom.createTextNode(TAGS))
        metadata.appendChild(tags)

        # Add projectUrl to metadata
        projectUrl: Element = dom.createElement('projectUrl')
        projectUrl.appendChild(dom.createTextNode(PROJECT_URL))
        metadata.appendChild(projectUrl)

        #Append repository to metadata
        repository: Element = dom.createElement('repository')
        repository.setAttribute('type', 'git')
        repository.setAttribute('url', REPOSITORY_URL)
        metadata.appendChild(repository)

        # Append ICON file to files
        iconfile: Element = dom.createElement('file')
        iconfile.setAttribute('src', os.path.join(CWD, ICON))
        iconfile.setAttribute('target', f'/{ICON}')
        files.appendChild(iconfile)

        xmlcontent = new_prettify(dom)
        print(xmlcontent)

        inp = input('\n Is the above corrent? (Y/N)')
        if inp.lower() != 'y':
            print('aborted')
            sys.exit(1)

        with open(filename, 'w') as fp:
            fp.write(xmlcontent)

    p = Popen([
        'nuget',
        'pack',
        filename,
        '-OutputDirectory',
        'ThingsDB/bin/Release/'], stdin=PIPE, stdout=PIPE, stderr=PIPE)
    output, err = p.communicate('')
    rc = p.returncode
    print('')
    if err:
        err = err.decode('UTF8')
        print(err)
        print('')
    output = output.decode('UTF8')
    print(output)


