import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";

export function MarkdownContent({
  className = "",
  content,
}: {
  className?: string;
  content: string;
}) {
  return (
    <div className={`markdown-content ${className}`.trim()}>
      <ReactMarkdown
        remarkPlugins={[remarkGfm]}
        components={{
          a: ({ children, ...props }) => (
            <a
              {...props}
              className="text-sage-200 underline decoration-sage-400/50 underline-offset-4 transition hover:text-white"
              rel="noreferrer"
              target="_blank"
            >
              {children}
            </a>
          ),
          code: ({ children, className, ...props }) => {
            const isBlock = className?.includes("language-");

            if (isBlock) {
              return (
                <code {...props} className={className}>
                  {children}
                </code>
              );
            }

            return (
              <code
                {...props}
                className="rounded-md bg-black/35 px-1.5 py-0.5 text-[0.95em] text-sage-100"
              >
                {children}
              </code>
            );
          },
        }}
      >
        {content}
      </ReactMarkdown>
    </div>
  );
}
