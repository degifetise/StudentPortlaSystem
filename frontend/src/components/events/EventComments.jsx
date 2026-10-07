import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { AlertCircle, ChevronDown, ChevronUp, MessageSquare, Reply, Send } from 'lucide-react';
import { eventApi } from '../../services/endpoints';
import { Alert, Spinner } from '../ui/Feedback';

const roleStyles = {
  Admin: 'bg-purple-100 text-purple-700',
  Teacher: 'bg-blue-100 text-blue-700',
  Student: 'bg-gray-100 text-gray-600',
};

function CommentItem({ comment, isAdmin, onReply, replyParentId, replyText, setReplyText, onSubmitReply, replySaving }) {
  const isReplying = replyParentId === comment.id;

  return (
    <li className="rounded-lg border border-slate-200 bg-white p-3">
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-sm font-semibold text-slate-800">{comment.userName}</span>
        <span className={`rounded-full px-2 py-0.5 text-[10px] font-semibold ${roleStyles[comment.userRole] ?? 'bg-slate-100 text-slate-700'}`}>
          {comment.userRole}
        </span>
        <time className="ml-auto text-[11px] text-slate-400" dateTime={comment.createdAt}>
          {new Date(comment.createdAt).toLocaleString()}
        </time>
      </div>
      <p className="mt-2 whitespace-pre-wrap text-sm leading-5 text-slate-700">{comment.commentText}</p>

      {isAdmin && comment.parentCommentId == null && (
        <button
          type="button"
          onClick={() => onReply(isReplying ? null : comment.id)}
          className="mt-2 inline-flex items-center gap-1 text-xs font-semibold text-brand-700 hover:text-brand-900"
        >
          <Reply className="size-3.5" aria-hidden="true" />
          {isReplying ? 'Cancel reply' : 'Reply'}
        </button>
      )}

      {isReplying && (
        <form onSubmit={(event) => onSubmitReply(event, comment.id)} className="mt-3 flex gap-2">
          <input
            className="input min-w-0 flex-1"
            value={replyText}
            onChange={(event) => setReplyText(event.target.value)}
            maxLength={2000}
            placeholder="Write an administrator reply"
            aria-label={`Reply to ${comment.userName}`}
            required
          />
          <button className="btn-primary shrink-0 px-3" type="submit" disabled={replySaving || !replyText.trim()}>
            {replySaving ? <Spinner className="size-4" /> : <Send className="size-4" aria-hidden="true" />}
            <span className="sr-only">Send reply</span>
          </button>
        </form>
      )}

      {comment.replies.length > 0 && (
        <ul className="mt-3 space-y-2 border-l-2 border-brand-100 pl-3">
          {comment.replies.map((reply) => (
            <li key={reply.id} className="rounded-lg bg-slate-50 px-3 py-2">
              <div className="flex flex-wrap items-center gap-2">
                <span className="text-xs font-semibold text-slate-800">{reply.userName}</span>
                <span className={`rounded-full px-2 py-0.5 text-[10px] font-semibold ${roleStyles[reply.userRole] ?? 'bg-slate-100 text-slate-700'}`}>
                  {reply.userRole}
                </span>
                <time className="ml-auto text-[10px] text-slate-400" dateTime={reply.createdAt}>
                  {new Date(reply.createdAt).toLocaleString()}
                </time>
              </div>
              <p className="mt-1 whitespace-pre-wrap text-sm text-slate-700">{reply.commentText}</p>
            </li>
          ))}
        </ul>
      )}
    </li>
  );
}

export default function EventComments({ eventId, canComment, isAdmin, userRole }) {
  const validEventId = Number.isInteger(Number(eventId)) && Number(eventId) > 0;
  const [isOpen, setIsOpen] = useState(false);
  const [comments, setComments] = useState([]);
  const [message, setMessage] = useState('');
  const [replyParentId, setReplyParentId] = useState(null);
  const [replyText, setReplyText] = useState('');
  const [loading, setLoading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [replySaving, setReplySaving] = useState(false);
  const [error, setError] = useState('');
  const hasLoadedComments = useRef(false);

  const loadComments = useCallback(async () => {
    if (!validEventId) return;
    setLoading(true);
    setError('');
    try {
      setComments(await eventApi.comments(eventId));
    } catch (loadError) {
      setError(loadError.friendlyMessage ?? 'Event comments could not be loaded.');
    } finally {
      setLoading(false);
    }
  }, [eventId, validEventId]);

  useEffect(() => {
    if (validEventId && (isOpen || !hasLoadedComments.current)) {
      hasLoadedComments.current = true;
      loadComments();
    }
  }, [isOpen, loadComments, validEventId]);

  const commentThreads = useMemo(() => {
    const repliesByParentId = new Map();
    const roots = [];
    comments.forEach((comment) => {
      if (comment.parentCommentId == null) {
        roots.push({ ...comment, replies: [] });
      } else {
        const replies = repliesByParentId.get(comment.parentCommentId) ?? [];
        replies.push(comment);
        repliesByParentId.set(comment.parentCommentId, replies);
      }
    });
    return roots.map((root) => ({
      ...root,
      replies: repliesByParentId.get(root.id) ?? [],
    }));
  }, [comments]);

  async function submitComment(event) {
    event.preventDefault();
    if (!validEventId || !message.trim()) return;
    setSaving(true);
    setError('');
    const temporaryId = `pending-${Date.now()}`;
    const pendingComment = {
      id: temporaryId,
      eventId: Number(eventId),
      userName: 'You',
      userRole,
      parentCommentId: null,
      commentText: message.trim(),
      createdAt: new Date().toISOString(),
    };
    setComments((current) => [...current, pendingComment]);
    const submittedMessage = message.trim();
    setMessage('');
    try {
      const comment = await eventApi.addComment(eventId, submittedMessage);
      setComments((current) => current.map((item) => item.id === temporaryId ? comment : item));
    } catch (submitError) {
      setComments((current) => current.filter((item) => item.id !== temporaryId));
      setMessage(submittedMessage);
      setError(submitError.friendlyMessage ?? 'Your comment could not be posted.');
    } finally {
      setSaving(false);
    }
  }

  async function submitReply(event, parentCommentId) {
    event.preventDefault();
    if (!validEventId || !replyText.trim()) return;
    setReplySaving(true);
    setError('');
    const temporaryId = `pending-${Date.now()}`;
    const submittedMessage = replyText.trim();
    const pendingReply = {
      id: temporaryId,
      eventId: Number(eventId),
      userName: 'You',
      userRole: 'Admin',
      parentCommentId,
      commentText: submittedMessage,
      createdAt: new Date().toISOString(),
    };
    setComments((current) => [...current, pendingReply]);
    setReplyText('');
    try {
      const reply = await eventApi.replyToComment(eventId, parentCommentId, submittedMessage);
      setComments((current) => current.map((item) => item.id === temporaryId ? reply : item));
      setReplyParentId(null);
    } catch (replyError) {
      setComments((current) => current.filter((item) => item.id !== temporaryId));
      setReplyText(submittedMessage);
      setError(replyError.friendlyMessage ?? 'The administrator reply could not be posted.');
    } finally {
      setReplySaving(false);
    }
  }

  if (!validEventId) return null;

  return (
    <section className="border-t border-slate-200 pt-3">
      <button
        type="button"
        onClick={() => setIsOpen((open) => !open)}
        className="flex w-full items-center justify-between rounded-lg px-2 py-2 text-sm font-semibold text-slate-700 transition-colors hover:bg-slate-50"
        aria-expanded={isOpen}
        aria-controls={`event-discussion-${eventId}`}
      >
        <span className="inline-flex items-center gap-2">
          <MessageSquare className="size-4 text-brand-600" aria-hidden="true" />
          Discussion ({comments.length})
        </span>
        {isOpen
          ? <ChevronUp className="size-4 text-slate-500" aria-hidden="true" />
          : <ChevronDown className="size-4 text-slate-500" aria-hidden="true" />}
      </button>

      {isOpen && (
        <div id={`event-discussion-${eventId}`} className="mt-2 rounded-xl border border-slate-200 bg-slate-50 p-3">
          {error && (
            <div role="alert" className="mb-3 flex items-center gap-2 rounded-lg bg-rose-50 px-3 py-2 text-xs text-rose-700">
              <AlertCircle className="size-4 shrink-0" aria-hidden="true" />
              <span>{error}</span>
            </div>
          )}
          {loading ? (
            <p className="py-4 text-center text-xs text-slate-500">Loading discussion…</p>
          ) : commentThreads.length ? (
            <ul className="max-h-60 space-y-3 overflow-y-auto">
              {commentThreads.map((comment) => (
                <CommentItem
                  key={comment.id}
                  comment={comment}
                  isAdmin={isAdmin}
                  onReply={setReplyParentId}
                  replyParentId={replyParentId}
                  replyText={replyText}
                  setReplyText={setReplyText}
                  onSubmitReply={submitReply}
                  replySaving={replySaving}
                />
              ))}
            </ul>
          ) : <p className="py-4 text-center text-xs text-slate-500">No comments yet. Start the discussion.</p>}

          {canComment && (
            <form onSubmit={submitComment} className="mt-3 flex gap-2">
              <input
                className="input min-w-0 flex-1 bg-white"
                value={message}
                onChange={(event) => setMessage(event.target.value)}
                maxLength={2000}
                placeholder={isAdmin ? 'Post a staff response' : 'Ask a question or leave a comment'}
                aria-label="Event comment"
                required
              />
              <button type="submit" className="btn-primary shrink-0 px-3" disabled={saving || !message.trim()}>
                {saving ? <Spinner className="size-4" /> : <Send className="size-4" aria-hidden="true" />}
                <span className="sr-only">Post comment</span>
              </button>
            </form>
          )}
        </div>
      )}
    </section>
  );
}
