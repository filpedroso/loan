import { LoanApplicationFlow } from "./_components/LoanApplicationFlow";
import { LogoMark } from "./_components/icons";

export default function Home() {
  return (
    <div className="flex min-h-full flex-1 flex-col">
      <header className="border-b border-slate-200 bg-white">
        <div className="mx-auto flex max-w-2xl items-center gap-3 px-4 py-4 sm:px-6">
          <LogoMark className="size-8" />
          <span className="font-semibold tracking-tight text-slate-900">Business Loans</span>
        </div>
      </header>

      <main className="mx-auto w-full max-w-2xl flex-1 px-4 py-8 sm:px-6 sm:py-12">
        <div className="rounded-2xl bg-white p-6 shadow-sm ring-1 ring-slate-200 sm:p-10">
          <LoanApplicationFlow />
        </div>
      </main>

      <footer className="px-4 pb-8 text-center text-xs text-slate-500">
        Demo application built as an engineering exercise. Please don’t enter real personal information.
      </footer>
    </div>
  );
}
