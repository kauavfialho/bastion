; Instalador do Bastion para Windows (NSIS).
; Instala só para o usuário atual em %LOCALAPPDATA%\Programs\bastion, sem pedir administrador,
; e aceita instalação silenciosa com /S (é assim que o Vitall Launcher instala e atualiza).
;
; Uso: makensis /DVERSAO=1.0.0 /DARQ=x64 /DORIGEM=..\publicar\x64\Bastion.exe /DSAIDA=..\dist\Bastion-win-x64.exe bastion.nsi

Unicode true
ManifestDPIAware true
RequestExecutionLevel user
SetCompressor /SOLID lzma

!ifndef VERSAO
  !define VERSAO "0.0.0"
!endif
!ifndef ARQ
  !define ARQ "x64"
!endif
!ifndef ORIGEM
  !error "Informe /DORIGEM=caminho\Bastion.exe"
!endif
!ifndef SAIDA
  !define SAIDA "Bastion-win-${ARQ}.exe"
!endif

!define NOME "Bastion"
!define PASTA "bastion"
!define EXE "Bastion.exe"
!define CHAVE_DESINSTALAR "Software\Microsoft\Windows\CurrentVersion\Uninstall\${PASTA}"

Name "${NOME}"
OutFile "${SAIDA}"
InstallDir "$LOCALAPPDATA\Programs\${PASTA}"
BrandingText "${NOME} ${VERSAO}"

VIProductVersion "${VERSAO}.0"
VIAddVersionKey /LANG=0 "ProductName" "${NOME}"
VIAddVersionKey /LANG=0 "ProductVersion" "${VERSAO}"
VIAddVersionKey /LANG=0 "FileVersion" "${VERSAO}"
VIAddVersionKey /LANG=0 "FileDescription" "Instalador do ${NOME}"
VIAddVersionKey /LANG=0 "CompanyName" "Kauã Fialho"
VIAddVersionKey /LANG=0 "LegalCopyright" "© 2026 Kauã Fialho · MIT"

!include "MUI2.nsh"
!include "x64.nsh"

!define MUI_ICON "..\Assets\app.ico"
!define MUI_UNICON "..\Assets\app.ico"
!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_RUN "$INSTDIR\${EXE}"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

; A primeira da lista é a reserva; o NSIS escolhe sozinho a que combina com o idioma do Windows
!insertmacro MUI_LANGUAGE "English"
!insertmacro MUI_LANGUAGE "PortugueseBR"
!insertmacro MUI_LANGUAGE "Spanish"

LangString TXT_FECHAR ${LANG_ENGLISH} "Closing Bastion…"
LangString TXT_FECHAR ${LANG_PORTUGUESEBR} "Fechando o Bastion…"
LangString TXT_FECHAR ${LANG_SPANISH} "Cerrando Bastion…"
LangString TXT_ARQ ${LANG_ENGLISH} "This installer is for Windows on ARM. Download Bastion-win-x64.exe for Intel or AMD computers."
LangString TXT_ARQ ${LANG_PORTUGUESEBR} "Este instalador é para Windows ARM. Baixe o Bastion-win-x64.exe para computadores Intel ou AMD."
LangString TXT_ARQ ${LANG_SPANISH} "Este instalador es para Windows ARM. Descarga Bastion-win-x64.exe para computadoras Intel o AMD."

Function .onInit
  !if "${ARQ}" == "arm64"
    ${IfNot} ${IsNativeARM64}
      MessageBox MB_ICONSTOP "$(TXT_ARQ)" /SD IDOK
      Abort
    ${EndIf}
  !endif
FunctionEnd

; Fecha o Bastion e o organizador (perto do relógio) para poder trocar o arquivo
!macro FecharBastion
  DetailPrint "$(TXT_FECHAR)"
  nsExec::Exec 'taskkill /IM "${EXE}" /F /T'
  Pop $0
  Sleep 800
!macroend

Section "Bastion" SecPrincipal
  !insertmacro FecharBastion
  SetOutPath "$INSTDIR"
  File "/oname=${EXE}" "${ORIGEM}"
  WriteUninstaller "$INSTDIR\Desinstalar Bastion.exe"

  CreateShortcut "$SMPROGRAMS\${NOME}.lnk" "$INSTDIR\${EXE}" "" "$INSTDIR\${EXE}" 0
  ; Na atualização silenciosa o atalho da Área de Trabalho não é recriado se a pessoa apagou
  IfSilent +2
  CreateShortcut "$DESKTOP\${NOME}.lnk" "$INSTDIR\${EXE}" "" "$INSTDIR\${EXE}" 0

  WriteRegStr HKCU "${CHAVE_DESINSTALAR}" "DisplayName" "${NOME}"
  WriteRegStr HKCU "${CHAVE_DESINSTALAR}" "DisplayVersion" "${VERSAO}"
  WriteRegStr HKCU "${CHAVE_DESINSTALAR}" "Publisher" "Kauã Fialho"
  WriteRegStr HKCU "${CHAVE_DESINSTALAR}" "DisplayIcon" "$INSTDIR\${EXE}"
  WriteRegStr HKCU "${CHAVE_DESINSTALAR}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${CHAVE_DESINSTALAR}" "URLInfoAbout" "https://github.com/kauavfialho/bastion"
  WriteRegStr HKCU "${CHAVE_DESINSTALAR}" "UninstallString" '"$INSTDIR\Desinstalar Bastion.exe"'
  WriteRegStr HKCU "${CHAVE_DESINSTALAR}" "QuietUninstallString" '"$INSTDIR\Desinstalar Bastion.exe" /S'
  WriteRegDWORD HKCU "${CHAVE_DESINSTALAR}" "NoModify" 1
  WriteRegDWORD HKCU "${CHAVE_DESINSTALAR}" "NoRepair" 1
SectionEnd

Section "Uninstall"
  !insertmacro FecharBastion
  Delete "$INSTDIR\${EXE}"
  Delete "$INSTDIR\Desinstalar Bastion.exe"
  RMDir "$INSTDIR"
  Delete "$SMPROGRAMS\${NOME}.lnk"
  Delete "$DESKTOP\${NOME}.lnk"
  ; O organizador deixa de abrir com o Windows. Configurações e registros em %LOCALAPPDATA%\Bastion ficam.
  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "Bastion Organizador"
  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "Bastion Luzes"
  DeleteRegKey HKCU "${CHAVE_DESINSTALAR}"
SectionEnd
